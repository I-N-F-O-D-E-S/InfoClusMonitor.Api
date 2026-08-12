using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Services;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Transfers;

public record CreateTransferCommand(StartTransferDto Request) : IRequest<FileTransfer>;

public class CreateTransferHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IMinioService minio,
    IMediator mediator,
    ILogger<CreateTransferHandler> logger) : IRequestHandler<CreateTransferCommand, FileTransfer>
{
    public async Task<FileTransfer> Handle(CreateTransferCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        // Validar servidor origen
        var sourceMachine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == req.SourceMachineId, ct);
        if (sourceMachine == null)
            throw new InvalidOperationException("El servidor de origen no existe.");
        if (sourceMachine.Status != MachineStatus.Online)
            throw new InvalidOperationException("El servidor de origen no está en línea.");

        // Validar servidor destino
        var targetMachine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == req.TargetMachineId, ct);
        if (targetMachine == null)
            throw new InvalidOperationException("El servidor de destino no existe.");
        if (targetMachine.Status != MachineStatus.Online)
            throw new InvalidOperationException("El servidor de destino no está en línea.");

        var transferId = Guid.NewGuid().ToString();
        var rawName = Path.GetFileName(req.SourcePath.TrimEnd('/', '\\'));
        if (string.IsNullOrWhiteSpace(rawName)) rawName = "backup";

        var objectName = $"transfers/{transferId}/{(req.IsDirectory ? rawName + ".tar.gz" : rawName)}";

        // Generar URL prefirmada PUT para subida temporal (válida por 2 horas)
        var uploadUrl = await minio.GetPresignedUploadUrlAsync(objectName, expirySeconds: 7200);

        var transfer = new FileTransfer
        {
            TransferId = transferId,
            SourceMachineId = sourceMachine.ExternalMachineId,
            SourceHostname = sourceMachine.Hostname,
            SourcePath = req.SourcePath,
            IsDirectory = req.IsDirectory,
            TargetMachineId = targetMachine.ExternalMachineId,
            TargetHostname = targetMachine.Hostname,
            TargetPath = req.TargetPath,
            MinioObjectName = objectName,
            Status = TransferStatus.Uploading,
            ProgressPercent = 10,
            CreatedAt = DateTime.UtcNow
        };

        db.FileTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Iniciando transferencia [{TransferId}] de {Source} ({Path}) a {Target} ({TargetPath})",
            transferId, sourceMachine.Hostname, req.SourcePath, targetMachine.Hostname, req.TargetPath);

        // Enviar orden de subida al servidor de origen vía RabbitMQ
        await rabbit.SendCustomCommandAsync(
            sourceMachine.ExternalMachineId,
            transferId,
            "TransferUpload",
            new
            {
                transferId = transferId,
                sourcePath = req.SourcePath,
                isDirectory = req.IsDirectory,
                uploadUrl = uploadUrl
            }
        );

        await mediator.Publish(new TransferCreatedNotification(transfer), ct);
        await mediator.Publish(new TransferUpdatedNotification(transfer), ct);

        return transfer;
    }
}
