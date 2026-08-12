using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Models.Dtos;

public record FileItemDto(
    string Name,
    string Path,
    bool IsDirectory,
    long Size,
    string SizeFormatted,
    DateTime? ModifiedAt,
    string? Permissions,
    string? Extension
);

public record DirectoryContentDto(
    string CurrentPath,
    string? ParentPath,
    List<FileItemDto> Items,
    string? Error = null
);

public record BrowseDirectoryRequestDto(
    string MachineId,
    string Path
);

public record StartTransferDto(
    string SourceMachineId,
    string SourcePath,
    bool IsDirectory,
    string TargetMachineId,
    string TargetPath
);

public record FileTransferDto(
    long Id,
    string TransferId,
    string SourceMachineId,
    string SourceHostname,
    string SourcePath,
    bool IsDirectory,
    string TargetMachineId,
    string TargetHostname,
    string TargetPath,
    TransferStatus Status,
    long SizeBytes,
    string SizeFormatted,
    int ProgressPercent,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record TransferProgressDto(
    string TransferId,
    string Status,
    int ProgressPercent,
    long SizeBytes,
    string? ErrorMessage
);

public record RequestDownloadDto(
    string MachineId,
    string Path,
    bool IsDirectory,
    List<string>? SelectedPaths = null
);

public record DownloadResultDto(
    string DownloadId,
    string FileName,
    string DownloadUrl,
    long SizeBytes,
    string? Error = null
);

