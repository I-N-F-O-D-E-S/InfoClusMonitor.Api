using InfoClusMonitor.Api.Commons;

namespace InfoClusMonitor.Api.Models.Entities;

public class ScheduledTaskExecution : BaseEntity
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// "Pending", "Running", "Completed", "Failed"
    /// </summary>
    public string Status { get; set; } = "Running";

    /// <summary>
    /// Salida de consola / stdout + stderr
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Mensaje de error si falló
    /// </summary>
    public string? ErrorMessage { get; set; }

    public int? ExitCode { get; set; }

    public long DurationMs { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }
}
