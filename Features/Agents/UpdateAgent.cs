using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Features.Commands;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.Agents;

public record UpdateAgentCommand(string MachineId) : IRequest<Command>;
public record UpdateAllAgentsCommand() : IRequest<List<Command>>;

public class UpdateAgentHandler(
    AppDbContext db,
    IMinioService minio,
    IMediator mediator,
    IWebHostEnvironment env,
    ILogger<UpdateAgentHandler> logger) : 
    IRequestHandler<UpdateAgentCommand, Command>,
    IRequestHandler<UpdateAllAgentsCommand, List<Command>>
{
    public const string PackageObjectName = "agent-package.tar.gz";

    public async Task EnsureReleasePackageInMinioAsync(CancellationToken ct = default)
    {
        // Buscar agent.py e install.sh en el repositorio
        var possibleAgentPaths = new[]
        {
            Path.Combine(env.ContentRootPath, "..", "..", "agent", "agent.py"),
            Path.Combine(env.ContentRootPath, "..", "agent", "agent.py"),
            Path.Combine(env.ContentRootPath, "agent", "agent.py"),
            Path.Combine(AppContext.BaseDirectory, "agent.py")
        };

        var possibleInstallPaths = new[]
        {
            Path.Combine(env.ContentRootPath, "..", "..", "agent", "install.sh"),
            Path.Combine(env.ContentRootPath, "..", "agent", "install.sh"),
            Path.Combine(env.ContentRootPath, "agent", "install.sh"),
            Path.Combine(AppContext.BaseDirectory, "install.sh")
        };

        var agentPath = possibleAgentPaths.FirstOrDefault(File.Exists);
        var installPath = possibleInstallPaths.FirstOrDefault(File.Exists);

        if (agentPath != null && installPath != null)
        {
            var agentContent = await File.ReadAllBytesAsync(agentPath, ct);
            var installContent = await File.ReadAllBytesAsync(installPath, ct);

            await CreateAndUploadTarGzPackageAsync(agentContent, installContent, ct);
            logger.LogInformation("Paquete unificado (agent.py + install.sh) empaquetado y subido a MinIO ({Bucket}) exitosamente.", minio.ReleasesBucketName);
        }
    }

    public async Task CreateAndUploadTarGzPackageAsync(byte[] agentBytes, byte[] installBytes, CancellationToken ct = default)
    {
        await minio.EnsurePublicReadBucketAsync(minio.ReleasesBucketName, ct);

        using var memoryStream = new MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzipStream))
        {
            // 1. Agregar agent.py al archivo tar
            var agentEntry = new PaxTarEntry(TarEntryType.RegularFile, "agent.py")
            {
                DataStream = new MemoryStream(agentBytes)
            };
            await tarWriter.WriteEntryAsync(agentEntry, ct);

            // 2. Agregar install.sh al archivo tar
            var installEntry = new PaxTarEntry(TarEntryType.RegularFile, "install.sh")
            {
                DataStream = new MemoryStream(installBytes)
            };
            await tarWriter.WriteEntryAsync(installEntry, ct);
        }

        memoryStream.Position = 0;
        await minio.UploadStreamAsync(PackageObjectName, memoryStream, memoryStream.Length, "application/gzip", bucketName: minio.ReleasesBucketName, ct: ct);
    }

    public async Task<Command> Handle(UpdateAgentCommand cmd, CancellationToken ct)
    {
        long.TryParse(cmd.MachineId, out var numericId);

        var machine = await db.Machines
            .FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.MachineId || (numericId > 0 && m.Id == numericId), ct);

        if (machine is null)
            throw new InvalidOperationException("Servidor no encontrado.");

        if (machine.Status != MachineStatus.Online)
            throw new InvalidOperationException("El servidor se encuentra fuera de línea.");

        await EnsureReleasePackageInMinioAsync(ct);

        // URL pública y permanente para descargar el paquete sin expiración ni firmas
        var packageDownloadUrl = minio.GetPublicUrl(PackageObjectName, minio.ReleasesBucketName);

        // El comando descarga el paquete, lo extrae y ejecuta install.sh
        var updateBashScript = $"mkdir -p /tmp/infoclus_agent_pkg && cd /tmp/infoclus_agent_pkg && curl -fsSL '{packageDownloadUrl}' -o package.tar.gz && tar -xzf package.tar.gz && chmod +x install.sh agent.py && bash install.sh";

        logger.LogInformation("Enviando orden de instalación/actualización con URL pública ({Url}) a {Hostname} ({MachineId})",
            packageDownloadUrl, machine.Hostname, machine.ExternalMachineId);

        return await mediator.Send(new CreateCommandCommand(machine.ExternalMachineId, updateBashScript), ct);
    }

    public async Task<List<Command>> Handle(UpdateAllAgentsCommand request, CancellationToken ct)
    {
        var onlineMachines = await db.Machines
            .Where(m => m.Status == MachineStatus.Online)
            .ToListAsync(ct);

        if (onlineMachines.Count == 0)
            return [];

        await EnsureReleasePackageInMinioAsync(ct);

        var results = new List<Command>();
        foreach (var machine in onlineMachines)
        {
            try
            {
                var cmd = await Handle(new UpdateAgentCommand(machine.ExternalMachineId), ct);
                results.Add(cmd);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fallo al enviar actualización con paquete unificado al nodo {Hostname}", machine.Hostname);
            }
        }

        return results;
    }
}
