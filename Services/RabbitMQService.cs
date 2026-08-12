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

public class RabbitMqService : IRabbitMqService
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqService> _logger;
    private readonly string _exchange = "infoclus.commands";
    private readonly string _eventsQueue;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private bool _isConsuming = false;

    public RabbitMqService(IConfiguration configuration, ILogger<RabbitMqService> logger)
    {
        _logger = logger;
        _eventsQueue = $"infoclus.backend.events.{Environment.MachineName.ToLowerInvariant()}_{Guid.NewGuid():N[..8]}";

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "45.10.154.37",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "infodes",
            Password = configuration["RabbitMQ:Password"] ?? "SydJqe93jV4o",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _logger.LogInformation("Conectando RabbitMqService a {Host}:{Port} con cola exclusiva {Queue}...", factory.HostName, factory.Port, _eventsQueue);

        _connection = factory.CreateConnectionAsync().Result;
        _channel = _connection.CreateChannelAsync().Result;

        _channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic, durable: true).Wait();
        _channel.QueueDeclareAsync(_eventsQueue, durable: false, exclusive: false, autoDelete: true).Wait();

        // Bind all event routing keys
        _channel.QueueBindAsync(_eventsQueue, _exchange, "register.#").Wait();
        _channel.QueueBindAsync(_eventsQueue, _exchange, "heartbeat.#").Wait();
        _channel.QueueBindAsync(_eventsQueue, _exchange, "result.#").Wait();
        _channel.QueueBindAsync(_eventsQueue, _exchange, "transfer.#").Wait();
        _channel.QueueBindAsync(_eventsQueue, _exchange, "filebrowse.#").Wait();

        _logger.LogInformation("Cola de eventos {Queue} vinculada al exchange {Exchange} exitosamente.", _eventsQueue, _exchange);
    }

    public void StartConsuming(
        Func<AgentRegisterDto, Task> onRegister,
        Func<string, AgentHeartbeatDto, Task> onHeartbeat,
        Func<CommandResultDto, Task> onResult,
        Func<string, string, Task>? onRawMessage = null)
    {
        if (_isConsuming) return;
        _isConsuming = true;

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            var routingKey = ea.RoutingKey;
            _logger.LogInformation("Mensaje recibido de RabbitMQ [{RoutingKey}]: {Json}", routingKey, json);

            try
            {
                if (onRawMessage != null)
                {
                    await onRawMessage(routingKey, json);
                }

                if (routingKey.StartsWith("register.", StringComparison.OrdinalIgnoreCase))
                {
                    var dto = JsonSerializer.Deserialize<AgentRegisterDto>(json, _jsonOptions);
                    if (dto != null)
                    {
                        var agentId = string.IsNullOrWhiteSpace(dto.AgentId) ? routingKey["register.".Length..] : dto.AgentId;
                        var finalDto = dto with { AgentId = agentId };
                        await onRegister(finalDto);
                    }
                }
                else if (routingKey.StartsWith("heartbeat.", StringComparison.OrdinalIgnoreCase))
                {
                    var dto = JsonSerializer.Deserialize<AgentHeartbeatDto>(json, _jsonOptions);
                    if (dto != null)
                    {
                        var agentId = !string.IsNullOrWhiteSpace(dto.AgentId) ? dto.AgentId : routingKey["heartbeat.".Length..];
                        await onHeartbeat(agentId, dto);
                    }
                }
                else if (routingKey.StartsWith("result.", StringComparison.OrdinalIgnoreCase))
                {
                    var result = JsonSerializer.Deserialize<CommandResultDto>(json, _jsonOptions);
                    if (result != null)
                    {
                        await onResult(result);
                    }
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar mensaje RabbitMQ para routing key: {RoutingKey}", routingKey);
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsumeAsync(_eventsQueue, autoAck: false, consumer: consumer).Wait();
        _logger.LogInformation("Consumidor de RabbitMQ iniciado para la cola {Queue}.", _eventsQueue);
    }

    public async Task SendCommandAsync(long commandId, string machineId, string parameters)
    {
        var routingKey = $"command.{machineId}";

        var body = JsonSerializer.Serialize(new
        {
            commandId = commandId.ToString(),
            machineId = machineId,
            type = "Exe",
            parameters = parameters
        });

        var bytes = Encoding.UTF8.GetBytes(body);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = commandId.ToString(),
            CorrelationId = commandId.ToString()
        };

        await _channel.BasicPublishAsync(_exchange, routingKey, true, props, bytes);
        _logger.LogInformation("Comando publicado a RabbitMQ [{RoutingKey}]: {CommandId} -> {Parameters}", routingKey, commandId, parameters);
    }

    public async Task SendCustomCommandAsync(string machineId, string commandId, string type, object parameters)
    {
        var routingKey = $"command.{machineId}";

        var body = JsonSerializer.Serialize(new
        {
            commandId = commandId,
            machineId = machineId,
            type = type,
            parameters = parameters is string str ? str : JsonSerializer.Serialize(parameters)
        });

        var bytes = Encoding.UTF8.GetBytes(body);

        var props = new BasicProperties
        {
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = commandId,
            CorrelationId = commandId
        };

        await _channel.BasicPublishAsync(_exchange, routingKey, true, props, bytes);
        _logger.LogInformation("Comando estructurado publicado a RabbitMQ [{RoutingKey}] tipo [{Type}]: {CommandId}", routingKey, type, commandId);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
