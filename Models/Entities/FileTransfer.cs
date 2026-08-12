using InfoClusMonitor.Api.Commons;
using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Models.Entities;

public class FileTransfer : BaseEntity
{
    public string TransferId { get; set; } = Guid.NewGuid().ToString();
    public string SourceMachineId { get; set; } = string.Empty;
    public string SourceHostname { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string TargetMachineId { get; set; } = string.Empty;
    public string TargetHostname { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string MinioObjectName { get; set; } = string.Empty;
    public TransferStatus Status { get; set; } = TransferStatus.Pending;
    public long SizeBytes { get; set; }
    public int ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
