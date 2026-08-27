using InfoClusMonitor.Api.Commons;

namespace InfoClusMonitor.Api.Models.Entities;

public class ScheduledTask : BaseEntity
{
    public string TaskId { get; set; } = Guid.NewGuid().ToString("N");
    public string MachineId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de programación: "EveryHours", "EveryDays", "SpecificDays", "Once", "CustomCron"
    /// </summary>
    public string ScheduleType { get; set; } = "EveryHours";

    /// <summary>
    /// Intervalo numérico (ej: 6 para cada 6 horas, 2 para cada 2 días)
    /// </summary>
    public int? IntervalValue { get; set; }

    /// <summary>
    /// Hora en formato "HH:mm" (ej: "03:00", "14:30") en horario de Paraguay (America/Asuncion)
    /// </summary>
    public string? ScheduledTime { get; set; }

    /// <summary>
    /// Días de la semana separados por coma (ej: "Monday,Wednesday,Friday" o "1,3,5")
    /// </summary>
    public string? DaysOfWeek { get; set; }

    /// <summary>
    /// Fecha específica para ejecución única (en UTC)
    /// </summary>
    public DateTime? SpecificDate { get; set; }

    /// <summary>
    /// Expresión cron personalizada estándar (opcional)
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Zona horaria objetivo (por defecto "America/Asuncion" / Horario Paraguayo)
    /// </summary>
    public string Timezone { get; set; } = "America/Asuncion";

    /// <summary>
    /// Si la tarea está activa o pausada
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Próxima ejecución programada (en UTC)
    /// </summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>
    /// Última ejecución realizada (en UTC)
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Estado de la última ejecución: "Pending", "Running", "Completed", "Failed"
    /// </summary>
    public string? LastStatus { get; set; }

    /// <summary>
    /// Resumen del último resultado
    /// </summary>
    public string? LastResult { get; set; }

    /// <summary>
    /// Duración de la última ejecución en milisegundos
    /// </summary>
    public long? LastDurationMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
