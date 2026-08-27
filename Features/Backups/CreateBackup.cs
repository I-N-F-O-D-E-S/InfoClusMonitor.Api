using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Backups;

public record CreateBackupCommand(CreateBackupDto Dto) : IRequest<BackupDto>;

public class CreateBackupHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IMinioService minio,
    IMediator mediator,
    ILogger<CreateBackupHandler> logger) : IRequestHandler<CreateBackupCommand, BackupDto>
{
    public async Task<BackupDto> Handle(CreateBackupCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        long.TryParse(dto.MachineId, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == dto.MachineId || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            throw new InvalidOperationException("Servidor no encontrado.");

        if (machine.Status != MachineStatus.Online)
            throw new InvalidOperationException("El servidor se encuentra fuera de línea.");

        if (string.IsNullOrWhiteSpace(dto.SourcePath))
            throw new ArgumentException("La ruta de origen es obligatoria.", nameof(dto.SourcePath));

        // Formatear nombre del respaldo: {FECHA}_{NOMBRE}.tar.gz
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        var rawName = string.IsNullOrWhiteSpace(dto.CustomName)
            ? Path.GetFileName(dto.SourcePath.TrimEnd('/', '\\'))
            : dto.CustomName.Trim();

        if (string.IsNullOrWhiteSpace(rawName))
            rawName = "backup";

        var cleanName = Regex.Replace(rawName, @"[^a-zA-Z0-9_\-]", "_");
        var fileName = $"{timestamp}_{cleanName}.tar.gz";
        var bucketName = minio.BackupsBucketName;
        var minioObjectName = $"{machine.Hostname}/{fileName}";

        // Asegurar que el bucket de copias de seguridad existe en MinIO
        await minio.EnsureBucketExistsAsync(bucketName, ct);

        // Generar URL prefirmada PUT para que el agente suba a MinIO (válida por 12 horas)
        var uploadUrl = await minio.GetPresignedUploadUrlAsync(minioObjectName, expirySeconds: 43200, bucketName: bucketName);

        var backup = new MachineBackup
        {
            BackupId = Guid.NewGuid().ToString("N"),
            MachineId = machine.ExternalMachineId,
            Hostname = machine.Hostname,
            SourcePath = dto.SourcePath,
            CustomName = rawName,
            FileName = fileName,
            MinioObjectName = minioObjectName,
            MinioBucket = bucketName,
            Status = BackupStatus.Pending,
            SizeBytes = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.Backups.Add(backup);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Copia de seguridad registrada [{BackupId}] para {Hostname} ({FileName}). Enviando orden a segundo plano...",
            backup.BackupId, machine.Hostname, fileName);

        // Notificar en tiempo real a la UI
        await mediator.Publish(new BackupCreatedNotification(backup), ct);

        // Enviar orden a RabbitMQ hacia el agente Linux (ejecución 100% asíncrona en segundo plano)
        await rabbit.SendCustomCommandAsync(
            machine.ExternalMachineId,
            backup.BackupId,
            "CreateBackup",
            new
            {
                backupId = backup.BackupId,
                sourcePath = dto.SourcePath,
                fileName = fileName,
                uploadUrl = uploadUrl,
                isDirectory = true
            }
        );

        return new BackupDto(
            backup.Id,
            backup.BackupId,
            backup.MachineId,
            backup.Hostname,
            backup.SourcePath,
            backup.CustomName,
            backup.FileName,
            backup.MinioBucket,
            backup.MinioObjectName,
            backup.Status,
            backup.SizeBytes,
            FormatSize(backup.SizeBytes),
            backup.ErrorMessage,
            backup.CreatedAt,
            backup.CompletedAt,
            null
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
