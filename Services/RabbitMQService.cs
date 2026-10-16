using System.Text;
using System.Text.Json;
using InfoClusMonitor.Api.Models.Dtos;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InfoClusMonitor.Api.Services;

public interface IRabbitMqService : IDisposable
{
    Task SendCommandAsync(long commandId, string machineId, string parameters);
    Task SendCustomCommandAsync(string machineId, string commandId, string type, object parameters);
    void StartConsuming(
        Func<AgentRegisterDto, Task> onRegister,
        Func<string, AgentHeartbeatDto, Task> onHeartbeat,
        Func<CommandResultDto, Task> onResult,
        Func<string, string, Task>? onRawMessage = null
    );
}

/// <summary>
/// Servicio de RabbitMQ resiliente: no tumba la aplicación si el broker no está
/// disponible al arrancar, se reconecta de forma continua y re-suscribe el
/// consumidor automáticamente tras cada reconexión. Los publishes reintentan
/// y fuerzan reconexión ante fallos de canal.
/// </summary>
public class RabbitMqService : IRabbitMqService
{
    private readonly ILogger<RabbitMqService> _logger;
    private readonly ConnectionFactory _factory;
    private readonly string _exchange = "infoclus.commands";
    private readonly string _eventsQueue;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly object _connectionLock = new();
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly Timer _maintenanceTimer;
    private readonly TimeSpan _maintenanceInterval = TimeSpan.FromSeconds(5);

    private IConnection? _connection;
    private IChannel? _channel;
    private IChannel? _consumerChannel;

    private volatile bool _isConsuming;
    private volatile bool _disposed;
    private int _maintaining;

    // Callbacks guardados para volver a suscribir el consumidor tras una reconexión
    private Func<AgentRegisterDto, Task>? _onRegister;
    private Func<string, AgentHeartbeatDto, Task>? _onHeartbeat;
    private Func<CommandResultDto, Task>? _onResult;
    private Func<string, string, Task>? _onRawMessage;

    private static readonly string[] EventRoutingKeys =
    [
        "register.#", "heartbeat.#", "result.#", "transfer.#", "filebrowse.#"
    ];

    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _logger = logger;
        _eventsQueue = $"infoclus.backend.events.{Environment.MachineName.ToLowerInvariant()}_{Guid.NewGuid().ToString("N")[..8]}";

        _factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "173.212.195.170",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "admin",
            Password = configuration["RabbitMQ:Password"] ?? "Maca*2023",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _logger.LogInformation("Iniciando RabbitMqService hacia {Host}:{Port} con cola de eventos {Queue}...",
            _factory.HostName, _factory.Port, _eventsQueue);

        // Conexión inicial tolerante a fallos: si el broker no responde, la API
        // arranca igual y el timer de mantenimiento reconnectsa más tarde.
        TryConnect();

        _maintenanceTimer = new Timer(_ => MaintainConnection(), null, _maintenanceInterval, _maintenanceInterval);
    }

    // ------------------------------------------------------------------
    // Conexión / reconexión
    // ------------------------------------------------------------------

    private bool TryConnect()
    {
        lock (_connectionLock)
        {
            if (_disposed) return false;

            _consumerChannel = null;
            try { _channel?.Dispose(); } catch { /* ignorado */ }
            try { _connection?.Dispose(); } catch { /* ignorado */ }
            _channel = null;
            _connection = null;

            IConnection? connection = null;
            IChannel? channel = null;

            try
            {
                connection = _factory.CreateConnectionAsync().GetAwaiter().GetResult();
                channel = connection.CreateChannelAsync().GetAwaiter().GetResult();

                channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
                channel.QueueDeclareAsync(_eventsQueue, durable: false, exclusive: false, autoDelete: true).GetAwaiter().GetResult();
                foreach (var key in EventRoutingKeys)
                {
                    channel.QueueBindAsync(_eventsQueue, _exchange, key).GetAwaiter().GetResult();
                }

                _connection = connection;
                _channel = channel;

                _logger.LogInformation("Conexión RabbitMQ establecida. Cola de eventos {Queue} vinculada al exchange {Exchange}.",
                    _eventsQueue, _exchange);
                return true;
            }
            catch (Exception ex)
            {
                try { channel?.Dispose(); } catch { /* ignorado */ }
                try { connection?.Dispose(); } catch { /* ignorado */ }
                _channel = null;
                _connection = null;

                _logger.LogError(ex, "No se pudo conectar a RabbitMQ ({Host}:{Port}). La API seguirá funcionando; se reintentará automáticamente.",
                    _factory.HostName, _factory.Port);
                return false;
            }
        }
    }

    /// <summary>Se ejecuta periódicamente en un hilo del pool. Nunca debe lanzar.</summary>
    private void MaintainConnection()
    {
        if (_disposed) return;
        if (Interlocked.CompareExchange(ref _maintaining, 1, 0) != 0) return;

        try
        {
            bool needsReconnect =
                _connection is null || !_connection.IsOpen ||
                _channel is null || !_channel.IsOpen;

            if (!needsReconnect) return;

            _logger.LogWarning("Conexión con RabbitMQ caída o no disponible. Intentando reconectar...");
            if (TryConnect() && _isConsuming)
            {
                StartConsumer();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el mantenimiento de la conexión RabbitMQ.");
        }
        finally
        {
            Interlocked.Exchange(ref _maintaining, 0);
        }
    }

    // ------------------------------------------------------------------
    // Consumo
    // ------------------------------------------------------------------

    public void StartConsuming(
        Func<AgentRegisterDto, Task> onRegister,
        Func<string, AgentHeartbeatDto, Task> onHeartbeat,
        Func<CommandResultDto, Task> onResult,
        Func<string, string, Task>? onRawMessage = null)
    {
        _onRegister = onRegister;
        _onHeartbeat = onHeartbeat;
        _onResult = onResult;
        _onRawMessage = onRawMessage;
        _isConsuming = true;

        try
        {
            if (_channel is null || !_channel.IsOpen)
            {
                TryConnect();
            }
            StartConsumer();
        }
        catch (Exception ex)
        {
            // Nunca propagar: el HostedService no debe caer. El timer reintentará.
            _logger.LogError(ex, "No se pudo iniciar el consumidor de RabbitMQ. Se reintentará automáticamente.");
        }
    }

    private void StartConsumer()
    {
        var channel = _channel;
        if (channel is null || !channel.IsOpen)
        {
            _logger.LogWarning("Canal de RabbitMQ no disponible; el consumidor se iniciará en la próxima reconexión.");
            return;
        }

        if (ReferenceEquals(_consumerChannel, channel))
        {
            return; // Ya hay un consumidor activo sobre este mismo canal
        }

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;
            _logger.LogInformation("Mensaje recibido de RabbitMQ [{RoutingKey}]: {Json}", routingKey, json);

            try
            {
                if (_onRawMessage != null)
                {
                    await _onRawMessage(routingKey, json);
                }

                if (routingKey.StartsWith("register.", StringComparison.OrdinalIgnoreCase))
                {
                    var dto = JsonSerializer.Deserialize<AgentRegisterDto>(json, _jsonOptions);
                    if (dto != null && _onRegister != null)
                    {
                        var agentId = string.IsNullOrWhiteSpace(dto.AgentId) ? routingKey["register.".Length..] : dto.AgentId;
                        var finalDto = dto with { AgentId = agentId };
                        await _onRegister(finalDto);
                    }
                }
                else if (routingKey.StartsWith("heartbeat.", StringComparison.OrdinalIgnoreCase))
                {
                    var dto = JsonSerializer.Deserialize<AgentHeartbeatDto>(json, _jsonOptions);
                    if (dto != null && _onHeartbeat != null)
                    {
                        var agentId = !string.IsNullOrWhiteSpace(dto.AgentId) ? dto.AgentId : routingKey["heartbeat.".Length..];
                        await _onHeartbeat(agentId, dto);
                    }
                }
                else if (routingKey.StartsWith("result.", StringComparison.OrdinalIgnoreCase))
                {
                    var result = JsonSerializer.Deserialize<CommandResultDto>(json, _jsonOptions);
                    if (result != null && _onResult != null)
                    {
                        await _onResult(result);
                    }
                }

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar mensaje RabbitMQ para routing key: {RoutingKey}", routingKey);
                try
                {
                    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                }
                catch (Exception nackEx)
                {
                    _logger.LogWarning(nackEx, "No se pudo rechazar (nack) el mensaje de RabbitMQ.");
                }
            }
        };

        channel.BasicConsumeAsync(_eventsQueue, autoAck: false, consumer: consumer).GetAwaiter().GetResult();
        _consumerChannel = channel;
        _logger.LogInformation("Consumidor de RabbitMQ iniciado para la cola {Queue}.", _eventsQueue);
    }

    // ------------------------------------------------------------------
    // Publicación con reintentos y reconexión
    // ------------------------------------------------------------------

    public Task SendCommandAsync(long commandId, string machineId, string parameters)
    {
        var routingKey = $"command.{machineId}";

        var body = JsonSerializer.Serialize(new
        {
            commandId = commandId.ToString(),
            machineId,
            type = "Exe",
            parameters
        });

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = commandId.ToString(),
            CorrelationId = commandId.ToString()
        };

        return PublishWithRetryAsync(routingKey, props, Encoding.UTF8.GetBytes(body), $"comando {commandId}");
    }

    public Task SendCustomCommandAsync(string machineId, string commandId, string type, object parameters)
    {
        var routingKey = $"command.{machineId}";

        var body = JsonSerializer.Serialize(new
        {
            commandId,
            machineId,
            type,
            parameters = parameters is string str ? str : JsonSerializer.Serialize(parameters)
        });

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = commandId,
            CorrelationId = commandId
        };

        return PublishWithRetryAsync(routingKey, props, Encoding.UTF8.GetBytes(body), $"comando {type} {commandId}");
    }

    private async Task PublishWithRetryAsync(string routingKey, BasicProperties props, byte[] bytes, string description)
    {
        const int maxAttempts = 3;

        // Serializa los publishes: IChannel no garantiza publicación concurrente segura.
        await _publishLock.WaitAsync();
        try
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (_channel is null || !_channel.IsOpen)
                    {
                        TryConnect();
                        if (_isConsuming) StartConsumer();
                    }

                    if (_channel is null || !_channel.IsOpen)
                    {
                        throw new InvalidOperationException("El canal de RabbitMQ no está disponible.");
                    }

                    await _channel.BasicPublishAsync(_exchange, routingKey, true, props, bytes);
                    _logger.LogInformation("Mensaje publicado a RabbitMQ [{RoutingKey}] ({Description}).", routingKey, description);
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex, "Fallo publicando {Description} en [{RoutingKey}]. Reintento {Attempt}/{Max}...",
                        description, routingKey, attempt, maxAttempts);

                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                    TryConnect();
                    if (_isConsuming) StartConsumer();
                }
            }
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        try { _maintenanceTimer?.Dispose(); } catch { /* ignorado */ }
        try { _consumerChannel?.Dispose(); } catch { /* ignorado */ }
        try { _channel?.Dispose(); } catch { /* ignorado */ }
        try { _connection?.Dispose(); } catch { /* ignorado */ }
        try { _publishLock.Dispose(); } catch { /* ignorado */ }
    }
}
