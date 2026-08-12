using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Features.Transfers;

public record GetAllTransfersQuery(string? MachineId = null) : IRequest<List<FileTransferDto>>;

public class GetAllTransfersHandler(AppDbContext db) : IRequestHandler<GetAllTransfersQuery, List<FileTransferDto>>
{
    public async Task<List<FileTransferDto>> Handle(GetAllTransfersQuery query, CancellationToken ct)
    {
        var dbQuery = db.FileTransfers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.MachineId))
        {
            dbQuery = dbQuery.Where(t => t.SourceMachineId == query.MachineId || t.TargetMachineId == query.MachineId);
        }

        var transfers = await dbQuery
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        return transfers.Select(t => new FileTransferDto(
            Id: t.Id,
            TransferId: t.TransferId,
            SourceMachineId: t.SourceMachineId,
            SourceHostname: t.SourceHostname,
            SourcePath: t.SourcePath,
            IsDirectory: t.IsDirectory,
            TargetMachineId: t.TargetMachineId,
            TargetHostname: t.TargetHostname,
            TargetPath: t.TargetPath,
            Status: t.Status,
            SizeBytes: t.SizeBytes,
            SizeFormatted: FormatSize(t.SizeBytes),
            ProgressPercent: t.ProgressPercent,
            ErrorMessage: t.ErrorMessage,
            CreatedAt: t.CreatedAt,
            CompletedAt: t.CompletedAt
        )).ToList();
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
