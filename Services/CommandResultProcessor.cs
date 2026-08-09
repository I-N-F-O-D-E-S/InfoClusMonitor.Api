using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Hubs;
using InfoClusMonitor.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InfoClusMonitor.Api.Services;

public class CommandResultProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRabbitMQService _rabbit;
    private readonly ILogger<CommandResultProcessor> _logger;

    public CommandResultProcessor(
        IServiceScopeFactory scopeFactory,
        IRabbitMQService rabbit,
        ILogger<CommandResultProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _rabbit = rabbit;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _rabbit.OnCommandResult += async result =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var hub = scope.ServiceProvider.GetRequiredService<IHubContext<MachineHub>>();

                var commandId = Guid.Parse(result.CommandId);
                var command = await db.Commands.FindAsync(commandId);
                if (command is null) return;

                var status = result.Status switch
                {
                    "Completed" => CommandStatus.Completed,
                    "Failed" => CommandStatus.Failed,
                    "Running" => CommandStatus.Running,
                    _ => CommandStatus.Failed
                };

                command.Status = status;
                command.Result = result.Result;
                if (status is CommandStatus.Completed or CommandStatus.Failed)
                {
                    command.CompletedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();

                await hub.Clients.Group($"machine-{command.MachineId}")
                    .SendAsync("CommandUpdated", command);

                _logger.LogInformation("Command {CommandId} updated to {Status}", commandId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command result");
            }
        };

        return Task.CompletedTask;
    }
}
