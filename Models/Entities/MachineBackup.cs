using InfoClusMonitor.Api.Commons;
using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Models.Entities;

public class MachineBackup : BaseEntity
{
    public string BackupId { get; set; } = Guid.NewGuid().ToString("N");
    public string MachineId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string CustomName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string MinioObjectName { get; set; } = string.Empty;
    public string MinioBucket { get; set; } = "copias-de-seguridad";
    public BackupStatus Status { get; set; } = BackupStatus.Pending;
    public long SizeBytes { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
