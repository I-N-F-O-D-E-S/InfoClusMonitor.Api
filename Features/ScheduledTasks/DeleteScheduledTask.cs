using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record DeleteScheduledTaskCommand(string TaskId) : IRequest<bool>;

public class DeleteScheduledTaskHandler(
    AppDbContext db,
    IMediator mediator,
    ILogger<DeleteScheduledTaskHandler> logger) : IRequestHandler<DeleteScheduledTaskCommand, bool>
{
    public async Task<bool> Handle(DeleteScheduledTaskCommand request, CancellationToken ct)
    {
        long.TryParse(request.TaskId, out var numericId);

        var task = await db.ScheduledTasks
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId || (numericId > 0 && t.Id == numericId), ct);

        if (task is null) return false;

        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Tarea programada eliminada [{TaskId}] '{Name}'", task.TaskId, task.Name);

        await mediator.Publish(new ScheduledTaskDeletedNotification(task.TaskId), ct);

        return true;
    }
}
