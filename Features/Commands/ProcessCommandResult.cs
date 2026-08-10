using MediatR;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Commands;

public record ProcessCommandResultCommand(
    string CommandId,
    string Status,
    string Result
) : IRequest;

public class ProcessCommandResultHandler(AppDbContext db, IMediator mediator)
    : IRequestHandler<ProcessCommandResultCommand>
{
    public async Task Handle(ProcessCommandResultCommand cmd, CancellationToken ct)
    {
        if (!long.TryParse(cmd.CommandId, out var commandId))
            return;

        var command = await db.Commands.FindAsync([commandId], ct);
        if (command is null)
            return;

        var status = cmd.Status switch
        {
            "Completed" => CommandStatus.Completed,
            "Failed" => CommandStatus.Failed,
            "Running" => CommandStatus.Running,
            _ => CommandStatus.Failed
        };

        command.Status = status;
        command.Result = cmd.Result;
        if (status is CommandStatus.Completed or CommandStatus.Failed)
        {
            command.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await mediator.Publish(new CommandUpdatedNotification(command), ct);
    }
}
