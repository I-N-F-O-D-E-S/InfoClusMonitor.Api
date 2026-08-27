using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record CreateScheduledTaskCommand(CreateScheduledTaskDto Dto) : IRequest<ScheduledTaskDto>;

public class CreateScheduledTaskHandler(
    AppDbContext db,
    IMediator mediator,
    ILogger<CreateScheduledTaskHandler> logger) : IRequestHandler<CreateScheduledTaskCommand, ScheduledTaskDto>
{
    public async Task<ScheduledTaskDto> Handle(CreateScheduledTaskCommand request, CancellationToken ct)
    {
        var dto = request.Dto;

        if (string.IsNullOrWhiteSpace(dto.MachineId))
            throw new ArgumentException("El ID del servidor es obligatorio.", nameof(dto.MachineId));

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("El nombre de la tarea programada es obligatorio.", nameof(dto.Name));

        if (string.IsNullOrWhiteSpace(dto.Command))
            throw new ArgumentException("El comando a ejecutar no puede estar vacío.", nameof(dto.Command));

        long.TryParse(dto.MachineId, out var numericId);
        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == dto.MachineId || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            throw new InvalidOperationException("Servidor no encontrado.");

        var task = new ScheduledTask
        {
            TaskId = Guid.NewGuid().ToString("N"),
            MachineId = machine.ExternalMachineId,
            Hostname = machine.Hostname,
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? string.Empty,
            Command = dto.Command.Trim(),
            ScheduleType = dto.ScheduleType ?? "EveryHours",
            IntervalValue = dto.IntervalValue ?? 1,
            ScheduledTime = dto.ScheduledTime,
            DaysOfWeek = dto.DaysOfWeek,
            SpecificDate = dto.SpecificDate,
            CronExpression = dto.CronExpression,
            Timezone = string.IsNullOrWhiteSpace(dto.Timezone) ? "America/Asuncion" : dto.Timezone,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Calcular primera fecha de ejecución
        task.NextRunAt = ScheduleCalculationHelper.CalculateNextRunAt(task, DateTime.UtcNow);

        db.ScheduledTasks.Add(task);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Tarea programada creada [{TaskId}] '{Name}' en {Hostname}. Próxima ejecución: {NextRun}",
            task.TaskId, task.Name, machine.Hostname, ScheduleCalculationHelper.FormatParaguayDateTime(task.NextRunAt));

        // Notificar creación en tiempo real
        await mediator.Publish(new ScheduledTaskCreatedNotification(task), ct);

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
