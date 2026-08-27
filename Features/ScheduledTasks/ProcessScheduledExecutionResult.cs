using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record ProcessScheduledExecutionResultCommand(
    string ExecutionId,
    string TaskId,
    string Status,
    string? Result,
    string? ErrorMessage,
    int? ExitCode,
    long DurationMs
) : IRequest<bool>;

public class ProcessScheduledExecutionResultHandler(
    AppDbContext db,
    IMediator mediator,
    ILogger<ProcessScheduledExecutionResultHandler> logger) : IRequestHandler<ProcessScheduledExecutionResultCommand, bool>
{
    public async Task<bool> Handle(ProcessScheduledExecutionResultCommand request, CancellationToken ct)
    {
        var execution = await db.ScheduledTaskExecutions
            .FirstOrDefaultAsync(e => e.ExecutionId == request.ExecutionId, ct);

        if (execution != null)
        {
            execution.Status = request.Status;
            execution.Result = request.Result;
            execution.ErrorMessage = request.ErrorMessage;
            execution.ExitCode = request.ExitCode;
            execution.DurationMs = request.DurationMs;
            execution.CompletedAt = DateTime.UtcNow;
        }

        var task = await db.ScheduledTasks
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId, ct);

        if (task != null)
        {
            task.LastStatus = request.Status;
            task.LastResult = request.Result ?? request.ErrorMessage;
            task.LastDurationMs = request.DurationMs;
            task.LastRunAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            // Recalcular próxima ejecución si está habilitada
            if (task.IsEnabled)
            {
                task.NextRunAt = ScheduleCalculationHelper.CalculateNextRunAt(task, DateTime.UtcNow);
            }
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Resultado de tarea programada procesado [{TaskId}] (Ejecución {ExecutionId}): {Status} ({DurationMs}ms)",
            request.TaskId, request.ExecutionId, request.Status, request.DurationMs);

        if (execution != null)
        {
            await mediator.Publish(new ScheduledExecutionUpdatedNotification(execution), ct);
        }

        if (task != null)
        {
            await mediator.Publish(new ScheduledTaskUpdatedNotification(task), ct);
        }

        return true;
    }
}
