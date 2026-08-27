using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Backups;

public record DeleteBackupCommand(string BackupId) : IRequest<bool>;

public class DeleteBackupHandler(
    AppDbContext db,
    IMinioService minio,
    IMediator mediator,
    ILogger<DeleteBackupHandler> logger) : IRequestHandler<DeleteBackupCommand, bool>
{
    public async Task<bool> Handle(DeleteBackupCommand request, CancellationToken ct)
    {
        long.TryParse(request.BackupId, out var numericId);

        var backup = await db.Backups
            .FirstOrDefaultAsync(b => b.BackupId == request.BackupId || (numericId > 0 && b.Id == numericId), ct);

        if (backup is null) return false;

        // Eliminar archivo del bucket en MinIO
        if (!string.IsNullOrEmpty(backup.MinioObjectName))
        {
            try
            {
                await minio.RemoveObjectAsync(backup.MinioObjectName, backup.MinioBucket, ct);
                logger.LogInformation("Copia de seguridad eliminada de MinIO: {Bucket}/{Object}", backup.MinioBucket, backup.MinioObjectName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error al eliminar archivo de respaldo de MinIO: {Object}", backup.MinioObjectName);
            }
        }

        backup.IsDeleted = true;
        await db.SaveChangesAsync(ct);

        // Notificar en tiempo real a los clientes conectados
        await mediator.Publish(new BackupDeletedNotification(backup.BackupId), ct);

        return true;
    }
}
