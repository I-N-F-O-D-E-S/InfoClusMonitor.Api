using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Transfers;

public record GetTransferByIdQuery(string TransferId) : IRequest<FileTransferDto?>;

public class GetTransferByIdHandler(AppDbContext db) : IRequestHandler<GetTransferByIdQuery, FileTransferDto?>
{
    public async Task<FileTransferDto?> Handle(GetTransferByIdQuery query, CancellationToken ct)
    {
        var t = await db.FileTransfers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransferId == query.TransferId, ct);

        if (t == null) return null;

        return new FileTransferDto(
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

public record CancelTransferCommand(string TransferId) : IRequest<bool>;

public class CancelTransferHandler(
    AppDbContext db,
    IMinioService minio,
    IMediator mediator) : IRequestHandler<CancelTransferCommand, bool>
{
    public async Task<bool> Handle(CancelTransferCommand cmd, CancellationToken ct)
    {
        var transfer = await db.FileTransfers
            .FirstOrDefaultAsync(x => x.TransferId == cmd.TransferId, ct);

        if (transfer == null) return false;

        if (transfer.Status == TransferStatus.Completed || transfer.Status == TransferStatus.Cancelled)
            return true;

        transfer.Status = TransferStatus.Cancelled;
        transfer.ErrorMessage = "Transferencia cancelada por el usuario.";
        transfer.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Limpieza de MinIO
        await minio.RemoveObjectAsync(transfer.MinioObjectName, ct: ct);

        await mediator.Publish(new TransferUpdatedNotification(transfer), ct);
        return true;
    }
}
