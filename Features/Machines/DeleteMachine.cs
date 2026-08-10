using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Machines;

public record DeleteMachineCommand(string Identifier) : IRequest<bool>;

public class DeleteMachineHandler(AppDbContext db, IMediator mediator) : IRequestHandler<DeleteMachineCommand, bool>
{
    public async Task<bool> Handle(DeleteMachineCommand cmd, CancellationToken ct)
    {
        long.TryParse(cmd.Identifier, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.Identifier || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            return false;

        var externalId = machine.ExternalMachineId;
        machine.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        await mediator.Publish(new MachineDeletedNotification(externalId), ct);
        return true;
    }
}
