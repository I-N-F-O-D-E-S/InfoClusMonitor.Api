using System.Text.Json;
using MediatR;
using InfoClusMonitor.Api.Features.Agents;
using InfoClusMonitor.Api.Features.Backups;
using InfoClusMonitor.Api.Features.Commands;
using InfoClusMonitor.Api.Features.Machines;
using InfoClusMonitor.Api.Features.Transfers;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Services;

public class AgentMessageProcessor(
    IServiceScopeFactory scopeFactory,
    IRabbitMqService rabbit,
    IFileBrowseManager browseManager,
    ILogger<AgentMessageProcessor> logger) : BackgroundService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Iniciando AgentMessageProcessor y suscribiendo eventos de RabbitMQ...");

        rabbit.StartConsuming(
            onRegister: async (AgentRegisterDto dto) =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new RegisterAgentCommand(
                        dto.AgentId, dto.Hostname, dto.Os, dto.IpAddress,
                        dto.PrivateIpAddress, dto.PublicIpAddress, dto.AgentVersion), stoppingToken);

                    logger.LogInformation("[✓] Agente registrado exitosamente vía RabbitMQ: {AgentId} ({Hostname})", dto.AgentId, dto.Hostname);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[X] Error al procesar registro de agente: {AgentId}", dto.AgentId);
                }
            },
            onHeartbeat: async (string agentId, AgentHeartbeatDto dto) =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(new SendHeartbeatCommand(
                        agentId, dto.AgentVersion, dto.Os, dto.IpAddress,
                        dto.PrivateIpAddress, dto.PublicIpAddress,
                        dto.CpuPercent, dto.MemoryPercent, dto.DiskPercent, dto.Uptime), stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[X] Error al procesar heartbeat para {AgentId}", agentId);
                }
            },
            onResult: async (CommandResultDto result) =>
            {
                try
                {
                    // 1. Verificar si es un resultado de BrowseFiles
                    if (!string.IsNullOrEmpty(result.Result) && result.Result.Contains("currentPath", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var dirContent = JsonSerializer.Deserialize<DirectoryContentDto>(result.Result, _jsonOptions);
                            if (dirContent != null)
                            {
                                browseManager.SetResult(result.CommandId, dirContent);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning("No se pudo deserializar resultado de explorador: {Message}", ex.Message);
                        }
                    }

                    // 2. Si el comando es un comando normal
                    if (long.TryParse(result.CommandId, out _))
                    {
                        using var scope = scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                        await mediator.Send(new ProcessCommandResultCommand(
                            result.CommandId, result.Status, result.Result), stoppingToken);

                        logger.LogInformation("[✓] Resultado de comando procesado: {CommandId} ({Status})",
                            result.CommandId, result.Status);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[X] Error al procesar resultado de comando: {CommandId}", result.CommandId);
                }
            },
            onRawMessage: async (string routingKey, string rawJson) =>
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    var root = doc.RootElement;

                    string? type = null;
                    if (root.TryGetProperty("type", out var typeProp) || root.TryGetProperty("Type", out typeProp))
                    {
                        type = typeProp.GetString();
                    }

                    string? commandId = null;
                    if (root.TryGetProperty("commandId", out var idProp) || root.TryGetProperty("transferId", out idProp) || root.TryGetProperty("CommandId", out idProp) || root.TryGetProperty("TransferId", out idProp))
                    {
                        commandId = idProp.GetString();
                    }

                    if (string.IsNullOrWhiteSpace(commandId)) return;

                    // Manejo de resultados de subida de transferencia a MinIO
                    if (string.Equals(type, "TransferUploadResult", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "Failed" : "Failed";
                        long sizeBytes = 0;
                        if (root.TryGetProperty("sizeBytes", out var sizeProp) && sizeProp.TryGetInt64(out var sz))
                        {
                            sizeBytes = sz;
                        }
                        var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;

                        using var scope = scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new ProcessTransferUploadCommand(commandId, status, sizeBytes, error), stoppingToken);
                    }
                    // Manejo de resultados de Copias de Seguridad (Backup)
                    else if (string.Equals(type, "BackupResult", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "Failed" : "Failed";
                        long sizeBytes = 0;
                        if (root.TryGetProperty("sizeBytes", out var sizeProp) && sizeProp.TryGetInt64(out var sz))
                        {
                            sizeBytes = sz;
                        }
                        var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;

                        using var scope = scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new ProcessBackupResultCommand(commandId, status, sizeBytes, error), stoppingToken);
                    }
                    // Manejo de resultados de descarga de transferencia desde MinIO
                    else if (string.Equals(type, "TransferDownloadResult", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "Failed" : "Failed";
                        var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;

                        using var scope = scopeFactory.CreateScope();
                        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new ProcessTransferDownloadCommand(commandId, status, error), stoppingToken);
                    }
                    // Manejo de resultados de preparación de descargas a MinIO
                    else if (string.Equals(type, "DownloadReadyResult", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "PrepareDownloadResult", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "Failed" : "Failed";
                        long sizeBytes = root.TryGetProperty("sizeBytes", out var szProp) && szProp.TryGetInt64(out var sz) ? sz : 0;
                        var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                        browseManager.SetDownloadResult(commandId, new RawDownloadResult(commandId, status, sizeBytes, error));
                    }
                    // Manejo de resultados de restauración de copias de seguridad
                    else if (string.Equals(type, "RestoreBackupResult", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "BackupRestoreResult", StringComparison.OrdinalIgnoreCase))
                    {
                        var status = root.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "Failed" : "Failed";
                        var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : null;
                        var targetPath = root.TryGetProperty("targetPath", out var tpProp) ? tpProp.GetString() : "";
                        logger.LogInformation("[✓] Resultado de restauración de respaldo: {CommandId} -> {Status} en '{TargetPath}' (Error: {Error})",
                            commandId, status, targetPath, error ?? "ninguno");
                    }
                    // Manejo de resultados de exploración de carpetas
                    else if (string.Equals(type, "BrowseFilesResult", StringComparison.OrdinalIgnoreCase))
                    {
                        if (root.TryGetProperty("result", out var resProp))
                        {
                            var resString = resProp.ValueKind == JsonValueKind.String ? resProp.GetString() : resProp.GetRawText();
                            if (!string.IsNullOrEmpty(resString))
                            {
                                var dirContent = JsonSerializer.Deserialize<DirectoryContentDto>(resString, _jsonOptions);
                                if (dirContent != null)
                                {
                                    browseManager.SetResult(commandId, dirContent);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error procesando mensaje raw en AgentMessageProcessor para {RoutingKey}", routingKey);
                }
            }
        );

        return Task.CompletedTask;
    }
}
