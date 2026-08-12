using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Transfers;

public record ProcessTransferDownloadCommand(
    string TransferId,
    string Status,
    string? ErrorMessage
) : IRequest<bool>;

public class ProcessTransferDownloadHandler(
    AppDbContext db,
    IMinioService minio,
    IMediator mediator,
    ILogger<ProcessTransferDownloadHandler> logger) : IRequestHandler<ProcessTransferDownloadCommand, bool>
{
    public async Task<bool> Handle(ProcessTransferDownloadCommand cmd, CancellationToken ct)
    {
        var transfer = await db.FileTransfers
            .FirstOrDefaultAsync(t => t.TransferId == cmd.TransferId, ct);

        if (transfer == null)
        {
            logger.LogWarning("Transferencia no encontrada para download result: {TransferId}", cmd.TransferId);
            return false;
        }

        // LIMPIEZA TEMPORAL: Borrar de MinIO el archivo utilizado para la transferencia
        try
        {
            await minio.RemoveObjectAsync(transfer.MinioObjectName, ct: ct);
            logger.LogInformation("[✓] Objeto temporal {Object} eliminado de MinIO exitosamente tras la transferencia.", transfer.MinioObjectName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Advertencia: no se pudo eliminar el objeto temporal {Object} de MinIO.", transfer.MinioObjectName);
        }

        if (cmd.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
            cmd.Status.Equals("Success", StringComparison.OrdinalIgnoreCase))
        {
            transfer.Status = TransferStatus.Completed;
            transfer.ProgressPercent = 100;
            transfer.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("[✓] ¡Transferencia [{TransferId}] COMPLETADA EXITOSAMENTE entre {Source} y {Target}!",
                transfer.TransferId, transfer.SourceHostname, transfer.TargetHostname);

            await mediator.Publish(new TransferUpdatedNotification(transfer), ct);
            return true;
        }
        else
        {
            logger.LogError("[X] Fallo en la descarga/extracción para [{TransferId}]: {Error}", transfer.TransferId, cmd.ErrorMessage);
            transfer.Status = TransferStatus.Failed;
            transfer.ErrorMessage = $"Error en descarga en servidor destino: {cmd.ErrorMessage}";
            transfer.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await mediator.Publish(new TransferUpdatedNotification(transfer), ct);
            return false;
        }
    }
}
