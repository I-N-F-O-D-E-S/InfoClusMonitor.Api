using System.Text;
using System.Text.Json;
using InfoClusMonitor.Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InfoClusMonitor.Api.Services;

public interface IRabbitMQService : IDisposable
{
    Task SendCommandAsync(Guid commandId, string machineId, string parameters);
    event Action<CommandResultDto>? OnCommandResult;
}

public class RabbitMQService : IRabbitMQService
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMQService> _logger;
    private readonly string _exchange = "infoclus.commands";
    private readonly string _resultsQueue = "infoclus.results";

    public event Action<CommandResultDto>? OnCommandResult;

    public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnectionAsync().Result;
        _channel = _connection.CreateChannelAsync().Result;

        _channel.ExchangeDeclareAsync(_exchange, ExchangeType.Topic, durable: true).Wait();
        _channel.QueueDeclareAsync(_resultsQueue, durable: true, exclusive: false, autoDelete: false).Wait();
        _channel.QueueBindAsync(_resultsQueue, _exchange, "result.*").Wait();

        StartConsumingResults();
    }

    private void StartConsumingResults()
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            _logger.LogInformation("Result received: {Json}", json);

            try
            {
                var result = JsonSerializer.Deserialize<CommandResultDto>(json);
                if (result != null)
                {
                    OnCommandResult?.Invoke(result);
                }
                await _channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing result");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        _channel.BasicConsumeAsync(_resultsQueue, autoAck: false, consumer: consumer).Wait();
    }

    public async Task SendCommandAsync(Guid commandId, string machineId, string parameters)
    {
        var routingKey = $"command.{machineId}";

        var body = JsonSerializer.Serialize(new
        {
            CommandId = commandId,
            MachineId = machineId,
            Type = "Exe",
            Parameters = parameters
        });

        var bytes = Encoding.UTF8.GetBytes(body);

        var props = new BasicProperties
        {
            Persistent = true,
            MessageId = commandId.ToString(),
            CorrelationId = commandId.ToString()
        };

        await _channel.BasicPublishAsync(_exchange, routingKey, true, props, bytes);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
