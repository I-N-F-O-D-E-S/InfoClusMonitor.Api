using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Models.Dtos;

public record CreateBackupDto(
    string MachineId,
    string SourcePath,
    string? CustomName
);

public record BackupDto(
    long Id,
    string BackupId,
    string MachineId,
    string Hostname,
    string SourcePath,
    string CustomName,
    string FileName,
    string MinioBucket,
    string MinioObjectName,
    BackupStatus Status,
    long SizeBytes,
    string SizeFormatted,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? DownloadUrl
);

public record RestoreBackupDto(
    string? TargetMachineId,
    string? TargetPath
);

public record RestoreBackupResultDto(
    string BackupId,
    string MachineId,
    string TargetPath,
    string Status,
    string? Message
);
