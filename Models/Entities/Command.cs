using System.Text.Json.Serialization;
using InfoClusMonitor.Api.Commons;
using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Models.Entities;

public class Command : BaseEntity
{
    public string ExternalMachineId { get; set; } = string.Empty;
    public string Type { get; set; } = "Exe";
    public string Parameters { get; set; } = string.Empty;
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [JsonIgnore]
    public Machine? Machine { get; set; }
}
