using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Backups;

public record ProcessBackupResultCommand(
    string BackupId,
    string Status,
    long SizeBytes,
    string? ErrorMessage
) : IRequest<bool>;

public class ProcessBackupResultHandler(
    AppDbContext db,
    IMediator mediator,
    ILogger<ProcessBackupResultHandler> logger) : IRequestHandler<ProcessBackupResultCommand, bool>
{
    public async Task<bool> Handle(ProcessBackupResultCommand request, CancellationToken ct)
    {
        long.TryParse(request.BackupId, out var numericId);

        var backup = await db.Backups
            .FirstOrDefaultAsync(b => b.BackupId == request.BackupId || (numericId > 0 && b.Id == numericId), ct);

        if (backup is null)
        {
            logger.LogWarning("No se encontró el registro de respaldo [{BackupId}] para actualizar resultado.", request.BackupId);
            return false;
        }

        if (string.Equals(request.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            backup.Status = BackupStatus.Completed;
            backup.SizeBytes = request.SizeBytes;
            backup.CompletedAt = DateTime.UtcNow;
            backup.ErrorMessage = null;
            logger.LogInformation("Copia de seguridad [{BackupId}] ({FileName}) completada exitosamente ({SizeBytes} bytes).",
                backup.BackupId, backup.FileName, backup.SizeBytes);
        }
        else
        {
            backup.Status = BackupStatus.Failed;
            backup.ErrorMessage = request.ErrorMessage ?? "Error al empaquetar o subir copia de seguridad a MinIO.";
            logger.LogWarning("Copia de seguridad [{BackupId}] falló: {Error}", backup.BackupId, backup.ErrorMessage);
        }

        await db.SaveChangesAsync(ct);

        // Notificar cambio de estado en tiempo real vía SignalR
        await mediator.Publish(new BackupUpdatedNotification(backup), ct);

        return true;
    }
}
