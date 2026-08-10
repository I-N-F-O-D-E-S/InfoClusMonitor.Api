using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Machines;

public record RefreshMachineTelemetryCommand(string Identifier) : IRequest<Machine?>;

public class RefreshMachineTelemetryHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IMediator mediator,
    ILogger<RefreshMachineTelemetryHandler> logger)
    : IRequestHandler<RefreshMachineTelemetryCommand, Machine?>
{
    public async Task<Machine?> Handle(RefreshMachineTelemetryCommand cmd, CancellationToken ct)
    {
        long.TryParse(cmd.Identifier, out var numericId);

        var machine = await db.Machines
            .Include(m => m.Commands.OrderByDescending(c => c.CreatedAt).Take(20))
            .FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.Identifier || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            return null;

        var command = new Command
        {
            ExternalMachineId = machine.ExternalMachineId,
            Type = "TelemetryRefresh",
            Parameters = "__refresh_telemetry__",
            Status = CommandStatus.Sent,
            CreatedAt = DateTime.UtcNow
        };

        db.Commands.Add(command);
        await db.SaveChangesAsync(ct);

        await mediator.Publish(new CommandCreatedNotification(command), ct);

        try
        {
            await rabbit.SendCommandAsync(command.Id, machine.ExternalMachineId, "__refresh_telemetry__");
            logger.LogInformation("Solicitud de telemetría en tiempo real enviada a nodo {MachineId}", machine.ExternalMachineId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al enviar comando de refresh a RabbitMQ para {MachineId}", machine.ExternalMachineId);
            command.Status = CommandStatus.Failed;
            command.Result = $"ERROR: No se pudo comunicar con RabbitMQ: {ex.Message}";
            await db.SaveChangesAsync(ct);
        }

        return machine;
    }
}
