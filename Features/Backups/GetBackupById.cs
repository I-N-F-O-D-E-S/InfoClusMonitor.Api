using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Backups;

public record GetBackupByIdQuery(string BackupId) : IRequest<BackupDto?>;

public class GetBackupByIdHandler(
    AppDbContext db,
    IMinioService minio) : IRequestHandler<GetBackupByIdQuery, BackupDto?>
{
    public async Task<BackupDto?> Handle(GetBackupByIdQuery request, CancellationToken ct)
    {
        long.TryParse(request.BackupId, out var numericId);

        var b = await db.Backups.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BackupId == request.BackupId || (numericId > 0 && x.Id == numericId), ct);

        if (b is null) return null;

        string? downloadUrl = null;
        if (b.Status == BackupStatus.Completed && !string.IsNullOrEmpty(b.MinioObjectName))
        {
            try
            {
                downloadUrl = await minio.GetPresignedDownloadUrlAsync(b.MinioObjectName, expirySeconds: 86400, bucketName: b.MinioBucket);
            }
            catch
            {
                // Ignore URL generation error
            }
        }

        return new BackupDto(
            b.Id,
            b.BackupId,
            b.MachineId,
            b.Hostname,
            b.SourcePath,
            b.CustomName,
            b.FileName,
            b.MinioBucket,
            b.MinioObjectName,
            b.Status,
            b.SizeBytes,
            FormatSize(b.SizeBytes),
            b.ErrorMessage,
            b.CreatedAt,
            b.CompletedAt,
            downloadUrl
        );
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
