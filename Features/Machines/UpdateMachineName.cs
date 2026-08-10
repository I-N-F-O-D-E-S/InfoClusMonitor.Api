using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Machines;

public record UpdateMachineNameCommand(string Identifier, string NewName) : IRequest<Machine?>;

public class UpdateMachineNameHandler(AppDbContext db, IMediator mediator)
    : IRequestHandler<UpdateMachineNameCommand, Machine?>
{
    public async Task<Machine?> Handle(UpdateMachineNameCommand cmd, CancellationToken ct)
    {
        long.TryParse(cmd.Identifier, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.Identifier || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            return null;

        machine.Name = cmd.NewName.Trim();
        machine.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await mediator.Publish(new MachineUpdatedNotification(machine), ct);
        return machine;
    }
}
