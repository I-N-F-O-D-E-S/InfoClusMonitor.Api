using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Transfers;

public record ProcessTransferUploadCommand(
    string TransferId,
    string Status,
    long SizeBytes,
    string? ErrorMessage
) : IRequest<bool>;

public class ProcessTransferUploadHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IMinioService minio,
    IMediator mediator,
    ILogger<ProcessTransferUploadHandler> logger) : IRequestHandler<ProcessTransferUploadCommand, bool>
{
    public async Task<bool> Handle(ProcessTransferUploadCommand cmd, CancellationToken ct)
    {
        var transfer = await db.FileTransfers
            .FirstOrDefaultAsync(t => t.TransferId == cmd.TransferId, ct);

        if (transfer == null)
        {
            logger.LogWarning("Transferencia no encontrada para upload result: {TransferId}", cmd.TransferId);
            return false;
        }

        if (cmd.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
            cmd.Status.Equals("Success", StringComparison.OrdinalIgnoreCase))
        {
            transfer.SizeBytes = cmd.SizeBytes;
            transfer.Status = TransferStatus.Downloading;
            transfer.ProgressPercent = 50;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Subida a MinIO completada para [{TransferId}] ({Size} bytes). Generando orden de descarga para {Target}...",
                transfer.TransferId, cmd.SizeBytes, transfer.TargetHostname);

            // Generar URL prefirmada GET para descarga (válida por 2 horas)
            var downloadUrl = await minio.GetPresignedDownloadUrlAsync(transfer.MinioObjectName, expirySeconds: 7200);

            // Enviar orden de descarga al servidor de destino
            await rabbit.SendCustomCommandAsync(
                transfer.TargetMachineId,
                transfer.TransferId,
                "TransferDownload",
                new
                {
                    transferId = transfer.TransferId,
                    targetPath = transfer.TargetPath,
                    isDirectory = transfer.IsDirectory,
                    downloadUrl = downloadUrl
                }
            );

            await mediator.Publish(new TransferUpdatedNotification(transfer), ct);
            return true;
        }
        else
        {
            logger.LogError("Fallo en la subida a MinIO para [{TransferId}]: {Error}", transfer.TransferId, cmd.ErrorMessage);
            transfer.Status = TransferStatus.Failed;
            transfer.ErrorMessage = $"Error en subida desde servidor origen: {cmd.ErrorMessage}";
            transfer.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            // Limpieza en MinIO si quedó algún archivo
            await minio.RemoveObjectAsync(transfer.MinioObjectName, ct: ct);

            await mediator.Publish(new TransferUpdatedNotification(transfer), ct);
            return false;
        }
    }
}
