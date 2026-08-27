using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Backups;

public record GetAllBackupsQuery(string? MachineId = null) : IRequest<List<BackupDto>>;

public class GetAllBackupsHandler(
    AppDbContext db,
    IMinioService minio) : IRequestHandler<GetAllBackupsQuery, List<BackupDto>>
{
    public async Task<List<BackupDto>> Handle(GetAllBackupsQuery request, CancellationToken ct)
    {
        var query = db.Backups.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.MachineId))
        {
            query = query.Where(b => b.MachineId == request.MachineId);
        }

        var backups = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        var result = new List<BackupDto>();

        foreach (var b in backups)
        {
            string? downloadUrl = null;
            if (b.Status == BackupStatus.Completed && !string.IsNullOrEmpty(b.MinioObjectName))
            {
                try
                {
                    downloadUrl = await minio.GetPresignedDownloadUrlAsync(b.MinioObjectName, expirySeconds: 86400, bucketName: b.MinioBucket);
                }
                catch
                {
                    // Ignore URL generation failure if object doesn't exist yet
                }
            }

            result.Add(new BackupDto(
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
            ));
        }

        return result;
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
