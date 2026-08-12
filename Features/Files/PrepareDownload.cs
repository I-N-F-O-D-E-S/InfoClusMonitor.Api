using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Files;

public record PrepareDownloadCommand(RequestDownloadDto Dto) : IRequest<DownloadResultDto>;

public class PrepareDownloadHandler(
    AppDbContext db,
    IRabbitMqService rabbit,
    IFileBrowseManager browseManager,
    IMinioService minio,
    ILogger<PrepareDownloadHandler> logger) : IRequestHandler<PrepareDownloadCommand, DownloadResultDto>
{
    public async Task<DownloadResultDto> Handle(PrepareDownloadCommand request, CancellationToken ct)
    {
        var dto = request.Dto;
        long.TryParse(dto.MachineId, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == dto.MachineId || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            return new DownloadResultDto("", "", "", 0, "Servidor no encontrado.");

        if (machine.Status != MachineStatus.Online)
            return new DownloadResultDto("", "", "", 0, "El servidor se encuentra fuera de línea.");

        var downloadId = Guid.NewGuid().ToString("N");
        
        // Determinar nombre del archivo y ruta de origen
        string fileName;
        string resolvedSourcePath;

        if (dto.SelectedPaths != null && dto.SelectedPaths.Count > 1)
        {
            fileName = $"archive_selected_{DateTime.UtcNow:yyyyMMdd_HHmmss}.tar.gz";
            resolvedSourcePath = dto.Path;
        }
        else if (dto.SelectedPaths != null && dto.SelectedPaths.Count == 1)
        {
            resolvedSourcePath = dto.SelectedPaths[0];
            var baseName = Path.GetFileName(resolvedSourcePath.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(baseName)) baseName = "item";
            fileName = dto.IsDirectory ? $"{baseName}.tar.gz" : baseName;
        }
        else if (dto.IsDirectory)
        {
            resolvedSourcePath = dto.Path;
            var baseName = Path.GetFileName(dto.Path.TrimEnd('/', '\\'));
            if (string.IsNullOrEmpty(baseName)) baseName = "root_folder";
            fileName = $"{baseName}.tar.gz";
        }
        else
        {
            resolvedSourcePath = dto.Path;
            fileName = Path.GetFileName(dto.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "downloaded_file";
        }

        var minioObjectName = $"downloads/{downloadId}/{fileName}";

        // Generar URL prefirmada PUT para que el agente suba a MinIO (válida por 2 horas)
        var uploadUrl = await minio.GetPresignedUploadUrlAsync(minioObjectName, expirySeconds: 7200);

        logger.LogInformation("Solicitando empaquetado/subida de descarga [{DownloadId}] para {MachineId} ({FileName}, source: {SourcePath})...",
            downloadId, machine.Hostname, fileName, resolvedSourcePath);

        var rawResult = await browseManager.RequestDownloadAsync(
            downloadId,
            async () =>
            {
                await rabbit.SendCustomCommandAsync(
                    machine.ExternalMachineId,
                    downloadId,
                    "PrepareDownload",
                    new
                    {
                        downloadId = downloadId,
                        sourcePath = resolvedSourcePath,
                        isDirectory = dto.IsDirectory,
                        selectedPaths = dto.SelectedPaths,
                        uploadUrl = uploadUrl,
                        targetFileName = fileName
                    }
                );
            },
            timeout: TimeSpan.FromSeconds(120), // 2 minutos para comprimir carpetas grandes
            ct
        );

        if (rawResult.Status != "Completed")
        {
            return new DownloadResultDto(
                DownloadId: downloadId,
                FileName: fileName,
                DownloadUrl: "",
                SizeBytes: 0,
                Error: rawResult.Error ?? "Error al empaquetar archivo en el servidor remoto."
            );
        }

        // Generar URL prefirmada GET de descarga directa para el navegador (válida por 2 horas)
        var downloadUrl = await minio.GetPresignedDownloadUrlAsync(minioObjectName, expirySeconds: 7200);

        logger.LogInformation("Descarga preparada exitosamente [{DownloadId}]: {FileName} ({SizeBytes} bytes)",
            downloadId, fileName, rawResult.SizeBytes);

        return new DownloadResultDto(
            DownloadId: downloadId,
            FileName: fileName,
            DownloadUrl: downloadUrl,
            SizeBytes: rawResult.SizeBytes,
            Error: null
        );
    }
}
