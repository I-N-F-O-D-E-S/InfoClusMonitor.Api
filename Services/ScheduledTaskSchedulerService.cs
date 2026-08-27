using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Models.Entities;
using MediatR;

namespace InfoClusMonitor.Api.Services;

public class ScheduledTaskSchedulerService(
    IServiceScopeFactory scopeFactory,
    IRabbitMqService rabbit,
    ILogger<ScheduledTaskSchedulerService> logger) : BackgroundService
{
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando ScheduledTaskSchedulerService (Motor de tareas programadas en segundo plano)...");

        // Pequeña espera inicial para permitir que las migraciones y conexiones terminen de inicializar
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueTasksAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error al evaluar tareas programadas en ScheduledTaskSchedulerService.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("ScheduledTaskSchedulerService detenido.");
    }

    private async Task ProcessDueTasksAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var nowUtc = DateTime.UtcNow;

        var dueTasks = await db.ScheduledTasks
            .Where(t => t.IsEnabled && t.NextRunAt != null && t.NextRunAt <= nowUtc)
            .ToListAsync(ct);

        if (dueTasks.Count == 0) return;

        logger.LogInformation("Se encontraron {Count} tarea(s) programada(s) listas para ejecutar a las {NowPy}...",
            dueTasks.Count, ScheduleCalculationHelper.FormatParaguayDateTime(nowUtc));

        foreach (var task in dueTasks)
        {
            try
            {
                var machine = await db.Machines
                    .FirstOrDefaultAsync(m => m.ExternalMachineId == task.MachineId, ct);

                if (machine == null)
                {
                    logger.LogWarning("Servidor [{MachineId}] no encontrado para la tarea programada [{TaskId}].", task.MachineId, task.TaskId);
                    task.LastStatus = "Failed";
                    task.LastResult = "Servidor no encontrado en la base de datos.";
                    task.NextRunAt = ScheduleCalculationHelper.CalculateNextRunAt(task, nowUtc);
                    continue;
                }

                if (machine.Status != MachineStatus.Online)
                {
                    logger.LogWarning("El servidor '{Hostname}' ({MachineId}) está desconectado. Pospone la ejecución de [{TaskId}] '{Name}'.",
                        machine.Hostname, machine.ExternalMachineId, task.TaskId, task.Name);
                    
                    // Reintentar en 2 minutos si el nodo está caído
                    task.NextRunAt = nowUtc.AddMinutes(2);
                    continue;
                }

                var execution = new ScheduledTaskExecution
                {
                    ExecutionId = Guid.NewGuid().ToString("N"),
                    TaskId = task.TaskId,
                    MachineId = machine.ExternalMachineId,
                    Hostname = machine.Hostname,
                    TaskName = task.Name,
                    Command = task.Command,
                    Status = "Running",
                    StartedAt = nowUtc
                };

                db.ScheduledTaskExecutions.Add(execution);

                task.LastRunAt = nowUtc;
                task.LastStatus = "Running";
                task.UpdatedAt = nowUtc;

                // Calcular siguiente ciclo
                task.NextRunAt = ScheduleCalculationHelper.CalculateNextRunAt(task, nowUtc);

                logger.LogInformation("Ejecutando tarea programada automática [{TaskId}] '{Name}' en {Hostname}. Próxima ejecución: {NextRun}",
                    task.TaskId, task.Name, machine.Hostname, ScheduleCalculationHelper.FormatParaguayDateTime(task.NextRunAt));

                // Guardar cambios antes de emitir RabbitMQ
                await db.SaveChangesAsync(ct);

                // Notificar en tiempo real
                await mediator.Publish(new ScheduledTaskUpdatedNotification(task), ct);
                await mediator.Publish(new ScheduledExecutionUpdatedNotification(execution), ct);

                // Enviar orden a RabbitMQ
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
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error al despachar tarea programada [{TaskId}]: {Message}", task.TaskId, ex.Message);
                task.NextRunAt = nowUtc.AddMinutes(5);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
