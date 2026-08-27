using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record UpdateScheduledTaskCommand(string TaskId, UpdateScheduledTaskDto Dto) : IRequest<ScheduledTaskDto>;

public class UpdateScheduledTaskHandler(
    AppDbContext db,
    IMediator mediator,
    ILogger<UpdateScheduledTaskHandler> logger) : IRequestHandler<UpdateScheduledTaskCommand, ScheduledTaskDto>
{
    public async Task<ScheduledTaskDto> Handle(UpdateScheduledTaskCommand request, CancellationToken ct)
    {
        long.TryParse(request.TaskId, out var numericId);

        var task = await db.ScheduledTasks
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId || (numericId > 0 && t.Id == numericId), ct);

        if (task is null)
            throw new InvalidOperationException("Tarea programada no encontrada.");

        var dto = request.Dto;

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("El nombre de la tarea es obligatorio.", nameof(dto.Name));

        if (string.IsNullOrWhiteSpace(dto.Command))
            throw new ArgumentException("El comando a ejecutar es obligatorio.", nameof(dto.Command));

        task.Name = dto.Name.Trim();
        task.Description = dto.Description?.Trim() ?? string.Empty;
        task.Command = dto.Command.Trim();
        task.ScheduleType = dto.ScheduleType ?? task.ScheduleType;
        task.IntervalValue = dto.IntervalValue ?? task.IntervalValue;
        task.ScheduledTime = dto.ScheduledTime;
        task.DaysOfWeek = dto.DaysOfWeek;
        task.SpecificDate = dto.SpecificDate;
        task.CronExpression = dto.CronExpression;
        task.Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "America/Asuncion" : dto.Timezone;
        task.UpdatedAt = DateTime.UtcNow;

        // Recalcular próxima ejecución
        task.NextRunAt = ScheduleCalculationHelper.CalculateNextRunAt(task, DateTime.UtcNow);

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Tarea programada actualizada [{TaskId}] '{Name}'. Próxima ejecución: {NextRun}",
            task.TaskId, task.Name, ScheduleCalculationHelper.FormatParaguayDateTime(task.NextRunAt));

        // Notificar actualización en tiempo real
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
