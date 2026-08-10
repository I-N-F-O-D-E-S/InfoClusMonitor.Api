using MediatR;
using InfoClusMonitor.Api.Features.Agents;
using InfoClusMonitor.Api.Features.Commands;
using InfoClusMonitor.Api.Features.Machines;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Services;

public class AgentMessageProcessor(
    IServiceScopeFactory scopeFactory,
    IRabbitMqService rabbit,
    ILogger<AgentMessageProcessor> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando AgentMessageProcessor y suscribiendo eventos de RabbitMQ...");

        rabbit.StartConsuming(
            onRegister: async (AgentRegisterDto dto) =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new RegisterAgentCommand(
                        dto.AgentId, dto.Hostname, dto.Os, dto.IpAddress,
                        dto.PrivateIpAddress, dto.PublicIpAddress, dto.AgentVersion), stoppingToken);

                    logger.LogInformation("[✓] Agente registrado exitosamente vía RabbitMQ: {AgentId} ({Hostname})", dto.AgentId, dto.Hostname);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[X] Error al procesar registro de agente: {AgentId}", dto.AgentId);
                }
            },
            onHeartbeat: async (string agentId, AgentHeartbeatDto dto) =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new SendHeartbeatCommand(
                        agentId, dto.AgentVersion, dto.Os, dto.IpAddress,
                        dto.PrivateIpAddress, dto.PublicIpAddress,
                        dto.CpuPercent, dto.MemoryPercent, dto.DiskPercent, dto.Uptime), stoppingToken);

                    logger.LogInformation("[✓] Heartbeat recibido y procesado para agente {AgentId} (CPU: {Cpu}%, RAM: {Ram}%)",
                        agentId, dto.CpuPercent, dto.MemoryPercent);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[X] Error al procesar heartbeat para {AgentId}", agentId);
                }
            },
            onResult: async (CommandResultDto result) =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new ProcessCommandResultCommand(
                        result.CommandId, result.Status, result.Result), stoppingToken);

                    logger.LogInformation("[✓] Resultado de comando procesado: {CommandId} ({Status})",
                        result.CommandId, result.Status);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[X] Error al procesar resultado de comando: {CommandId}", result.CommandId);
                }
            }
        );

        return Task.CompletedTask;
    }
}
