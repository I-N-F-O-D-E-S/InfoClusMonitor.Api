using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Backups;

public record RestoreBackupCommand(string BackupId, RestoreBackupDto Dto) : IRequest<RestoreBackupResultDto>;

public class RestoreBackupHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IMinioService minio,
    ILogger<RestoreBackupHandler> logger) : IRequestHandler<RestoreBackupCommand, RestoreBackupResultDto>
{
    public async Task<RestoreBackupResultDto> Handle(RestoreBackupCommand request, CancellationToken ct)
    {
        long.TryParse(request.BackupId, out var numericId);

        var backup = await db.Backups
            .FirstOrDefaultAsync(b => b.BackupId == request.BackupId || (numericId > 0 && b.Id == numericId), ct);

        if (backup is null)
            throw new InvalidOperationException("Copia de seguridad no encontrada.");

        if (backup.Status != BackupStatus.Completed)
            throw new InvalidOperationException("La copia de seguridad no está en estado completado o disponible para restaurar.");

        var targetMachineId = !string.IsNullOrWhiteSpace(request.Dto.TargetMachineId)
            ? request.Dto.TargetMachineId
            : backup.MachineId;

        long.TryParse(targetMachineId, out var targetNumericId);
        var targetMachine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == targetMachineId || (targetNumericId > 0 && m.Id == targetNumericId), ct);

        if (targetMachine is null)
            throw new InvalidOperationException("El servidor de destino no fue encontrado.");

        if (targetMachine.Status != MachineStatus.Online)
            throw new InvalidOperationException($"El servidor de destino ({targetMachine.Hostname}) se encuentra fuera de línea.");

        var targetPath = !string.IsNullOrWhiteSpace(request.Dto.TargetPath)
            ? request.Dto.TargetPath.Trim()
            : backup.SourcePath;

        if (string.IsNullOrWhiteSpace(targetPath))
            targetPath = "/";

        // Generar URL prefirmada GET válida para descargar el respaldo desde MinIO
        var downloadUrl = await minio.GetPresignedDownloadUrlAsync(backup.MinioObjectName, expirySeconds: 43200, bucketName: backup.MinioBucket);

        logger.LogInformation("Enviando orden de restauración de respaldo [{BackupId}] ({FileName}) hacia {Hostname} en la ruta '{TargetPath}'",
            backup.BackupId, backup.FileName, targetMachine.Hostname, targetPath);

        // Enviar orden estructurada por RabbitMQ al agente
        await rabbit.SendCustomCommandAsync(
            targetMachine.ExternalMachineId,
            Guid.NewGuid().ToString("N"),
            "RestoreBackup",
            new
            {
                backupId = backup.BackupId,
                downloadUrl = downloadUrl,
                targetPath = targetPath,
                fileName = backup.FileName
            }
        );

        return new RestoreBackupResultDto(
            backup.BackupId,
            targetMachine.ExternalMachineId,
            targetPath,
            "Dispatched",
            $"Orden de restauración enviada exitosamente al servidor {targetMachine.Hostname}."
        );
    }
}
