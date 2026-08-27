using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record ToggleScheduledTaskCommand(string TaskId) : IRequest<ScheduledTaskDto>;

public class ToggleScheduledTaskHandler(
    AppDbContext db,
    IMediator mediator,
    ILogger<ToggleScheduledTaskHandler> logger) : IRequestHandler<ToggleScheduledTaskCommand, ScheduledTaskDto>
{
    public async Task<ScheduledTaskDto> Handle(ToggleScheduledTaskCommand request, CancellationToken ct)
    {
        long.TryParse(request.TaskId, out var numericId);

        var task = await db.ScheduledTasks
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId || (numericId > 0 && t.Id == numericId), ct);

        if (task is null)
            throw new InvalidOperationException("Tarea programada no encontrada.");

        task.IsEnabled = !task.IsEnabled;
        task.UpdatedAt = DateTime.UtcNow;

        if (task.IsEnabled)
        {
            task.NextRunAt = ScheduleCalculationHelper.CalculateNextRunAt(task, DateTime.UtcNow);
        }
        else
        {
            task.NextRunAt = null;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Tarea programada [{TaskId}] '{Name}' cambiada a: {State}",
            task.TaskId, task.Name, task.IsEnabled ? "ACTIVA" : "PAUSADA");

        await mediator.Publish(new ScheduledTaskUpdatedNotification(task), ct);

        return new ScheduledTaskDto(
            task.Id,
            task.TaskId,
            task.MachineId,
            task.Hostname,
            task.Name,
            task.Description,
            task.Command,
            task.ScheduleType,
            task.IntervalValue,
            task.ScheduledTime,
            task.DaysOfWeek,
            task.SpecificDate,
            task.CronExpression,
            task.Timezone,
            task.IsEnabled,
            task.NextRunAt,
            task.LastRunAt,
            task.LastStatus,
            task.LastResult,
            task.LastDurationMs,
            task.CreatedAt,
            task.UpdatedAt,
            ScheduleCalculationHelper.GenerateScheduleSummary(task),
            ScheduleCalculationHelper.FormatParaguayDateTime(task.NextRunAt),
            ScheduleCalculationHelper.FormatParaguayDateTime(task.LastRunAt)
        );
    }
}
