using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record RunScheduledTaskNowCommand(string TaskId) : IRequest<bool>;

public class RunScheduledTaskNowHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IMediator mediator,
    ILogger<RunScheduledTaskNowHandler> logger) : IRequestHandler<RunScheduledTaskNowCommand, bool>
{
    public async Task<bool> Handle(RunScheduledTaskNowCommand request, CancellationToken ct)
    {
        long.TryParse(request.TaskId, out var numericId);

        var task = await db.ScheduledTasks
            .FirstOrDefaultAsync(t => t.TaskId == request.TaskId || (numericId > 0 && t.Id == numericId), ct);

        if (task is null)
            throw new InvalidOperationException("Tarea programada no encontrada.");

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == task.MachineId, ct);

        if (machine is null)
            throw new InvalidOperationException("El servidor asociado a esta tarea no fue encontrado.");

        if (machine.Status != MachineStatus.Online)
            throw new InvalidOperationException($"El servidor ({machine.Hostname}) se encuentra fuera de línea.");

        var execution = new ScheduledTaskExecution
        {
            ExecutionId = Guid.NewGuid().ToString("N"),
            TaskId = task.TaskId,
            MachineId = machine.ExternalMachineId,
            Hostname = machine.Hostname,
            TaskName = task.Name,
            Command = task.Command,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };

        db.ScheduledTaskExecutions.Add(execution);

        task.LastStatus = "Running";
        task.LastRunAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Lanzando ejecución manual de tarea programada [{TaskId}] '{Name}' en {Hostname}...",
            task.TaskId, task.Name, machine.Hostname);

        // Notificar inicio en tiempo real
        await mediator.Publish(new ScheduledTaskUpdatedNotification(task), ct);
        await mediator.Publish(new ScheduledExecutionUpdatedNotification(execution), ct);

        // Enviar orden a RabbitMQ hacia el agente Linux
        await rabbit.SendCustomCommandAsync(
            machine.ExternalMachineId,
            execution.ExecutionId,
            "ScheduledCommand",
            new
            {
                executionId = execution.ExecutionId,
                taskId = task.TaskId,
                command = task.Command,
                name = task.Name
            }
        );

        return true;
    }
}
