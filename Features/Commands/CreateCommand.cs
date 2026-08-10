using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Commands;

public record CreateCommandCommand(
    string MachineId,
    string Parameters
) : IRequest<Command>;

public class CreateCommandHandler(AppDbContext db, IRabbitMqService rabbit, IMediator mediator)
    : IRequestHandler<CreateCommandCommand, Command>
{
    public async Task<Command> Handle(CreateCommandCommand cmd, CancellationToken ct)
    {
        long.TryParse(cmd.MachineId, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.MachineId || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            throw new InvalidOperationException("Servidor no encontrado.");

        if (machine.Status != MachineStatus.Online)
            throw new InvalidOperationException("El servidor no se encuentra en línea.");

        var command = new Command
        {
            ExternalMachineId = machine.ExternalMachineId,
            Type = "Exe",
            Parameters = cmd.Parameters,
            Status = CommandStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        db.Commands.Add(command);
        await db.SaveChangesAsync(ct);

        await rabbit.SendCommandAsync(command.Id, machine.ExternalMachineId, cmd.Parameters);

        command.Status = CommandStatus.Sent;
        command.ExecutedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await mediator.Publish(new CommandCreatedNotification(command), ct);
        return command;
    }
}
