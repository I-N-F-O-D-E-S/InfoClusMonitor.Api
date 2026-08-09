using System.Text.Json.Serialization;

namespace InfoClusMonitor.Api.Models;

public enum CommandStatus
{
    Pending,
    Sent,
    Running,
    Completed,
    Failed
}

public class Command
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MachineId { get; set; } = string.Empty;
    public string Type { get; set; } = "Exe";
    public string Parameters { get; set; } = string.Empty;
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [JsonIgnore]
    public Machine Machine { get; set; } = null!;
}
