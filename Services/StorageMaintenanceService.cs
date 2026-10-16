using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Commons.Enums;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Services;

/// <summary>
/// Servicio de mantenimiento en segundo plano:
///  1) Marca como fallidas las transferencias colgadas (nodo que murió a mitad) y limpia su objeto temporal.
///  2) Elimina de MinIO los objetos temporales huérfanos (descargas al navegador y transferencias).
/// Nunca lanza excepciones hacia el host: cualquier error se registra y se reintenta en el siguiente ciclo.
/// </summary>
public class StorageMaintenanceService(
    IServiceScopeFactory scopeFactory,
    IMinioService minio,
    IConfiguration configuration,
    ILogger<StorageMaintenanceService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(
        int.TryParse(configuration["Maintenance:IntervalMinutes"], out var m) && m > 0 ? m : 30);

    private readonly TimeSpan _staleTransferThreshold = TimeSpan.FromMinutes(
        int.TryParse(configuration["Maintenance:StaleTransferMinutes"], out var s) && s > 0 ? s : 360);

    private readonly TimeSpan _downloadsMaxAge = TimeSpan.FromHours(
        int.TryParse(configuration["Maintenance:DownloadsMaxAgeHours"], out var dh) && dh > 0 ? dh : 6);

    private readonly TimeSpan _transfersMaxAge = TimeSpan.FromHours(
        int.TryParse(configuration["Maintenance:TransfersMaxAgeHours"], out var th) && th > 0 ? th : 24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "StorageMaintenanceService iniciado (cada {Interval}min · transferencias colgadas > {Stale}min · descargas > {DL}h · transferencias MinIO > {TR}h).",
            _interval.TotalMinutes, _staleTransferThreshold.TotalMinutes, _downloadsMaxAge.TotalHours, _transfersMaxAge.TotalHours);

        // Pequeña espera inicial para no competir con el arranque
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error durante el ciclo de mantenimiento de almacenamiento.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("StorageMaintenanceService detenido.");
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await ReapStaleTransfersAsync(ct);

        // Objetos temporales de descargas directas al navegador (nunca se borran tras descargar)
        await minio.RemoveObjectsOlderThanAsync("downloads/", _downloadsMaxAge, minio.BucketName, ct);

        // Objetos temporales de transferencias que quedaron huérfanos (fallidas o interrumpidas)
        await minio.RemoveObjectsOlderThanAsync("transfers/", _transfersMaxAge, minio.BucketName, ct);
    }

    private async Task ReapStaleTransfersAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var cutoffUtc = DateTime.UtcNow - _staleTransferThreshold;

        var stale = await db.FileTransfers
            .Where(t => t.CreatedAt < cutoffUtc &&
                        (t.Status == TransferStatus.Pending ||
                         t.Status == TransferStatus.Uploading ||
                         t.Status == TransferStatus.Uploaded ||
                         t.Status == TransferStatus.Downloading))
            .ToListAsync(ct);

        if (stale.Count == 0) return;

        logger.LogWarning("Se encontraron {Count} transferencia(s) colgadas sin resultado del agente. Marcando como fallidas.", stale.Count);

        foreach (var transfer in stale)
        {
            transfer.Status = TransferStatus.Failed;
            transfer.ErrorMessage = "Transferencia finalizada por inactividad: el agente no reportó resultado dentro del tiempo límite.";
            transfer.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        foreach (var transfer in stale)
        {
            // Limpieza del objeto temporal en MinIO (si quedó algo subido)
            await minio.RemoveObjectAsync(transfer.MinioObjectName, minio.BucketName, ct);

            try
            {
                await mediator.Publish(new TransferUpdatedNotification(transfer), ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo notificar la finalización de la transferencia colgada {TransferId}.", transfer.TransferId);
            }
        }
    }
}
