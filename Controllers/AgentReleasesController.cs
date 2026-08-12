using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Agents;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentReleasesController(IMediator mediator, IMinioService minio) : ControllerBase
{
    private async Task<string> GenerateInstallCommandAsync(int expirySeconds = 604800)
    {
        var presignedUrl = await minio.GetPresignedDownloadUrlAsync(UpdateAgentHandler.PackageObjectName, expirySeconds);
        return $"curl -fsSL '{presignedUrl}' -o /tmp/pkg.tar.gz && cd /tmp && tar -xzf pkg.tar.gz && sudo bash install.sh";
    }

    [HttpGet("install-command")]
    public async Task<ActionResult> GetInstallCommand()
    {
        var packageExists = await minio.ObjectExistsAsync(UpdateAgentHandler.PackageObjectName);
        var command = await GenerateInstallCommandAsync();

        return Ok(new
        {
            packageAvailable = packageExists,
            installCommand = command,
            targetVersion = "1.1.0"
        });
    }

    [HttpPost("upload-bundle")]
    public async Task<ActionResult> UploadReleaseBundle(IFormFile agentFile, IFormFile installFile)
    {
        if (agentFile == null || agentFile.Length == 0)
            return BadRequest("El archivo agent.py es obligatorio.");

        if (installFile == null || installFile.Length == 0)
            return BadRequest("El archivo install.sh es obligatorio.");

        byte[] agentBytes;
        using (var ms = new MemoryStream())
        {
            await agentFile.CopyToAsync(ms);
            agentBytes = ms.ToArray();
        }

        byte[] installBytes;
        using (var ms = new MemoryStream())
        {
            await installFile.CopyToAsync(ms);
            installBytes = ms.ToArray();
        }

        // Empaquetar ambos archivos juntos en un único .tar.gz y subir a MinIO
        var handler = new UpdateAgentHandler(null!, minio, mediator, null!, null!);
        await handler.CreateAndUploadTarGzPackageAsync(agentBytes, installBytes);

        var installCommand = await GenerateInstallCommandAsync();

        return Ok(new
        {
            message = "Paquete (agent.py + install.sh) subido y empaquetado exitosamente en MinIO.",
            installCommand = installCommand,
            agentSize = agentFile.Length,
            installSize = installFile.Length
        });
    }

    [HttpPost("deploy-all")]
    public async Task<ActionResult> DeployToAll()
    {
        var results = await mediator.Send(new UpdateAllAgentsCommand());
        return Ok(new
        {
            updatedCount = results.Count,
            message = $"Comando enviado a {results.Count} servidores en línea vía RabbitMQ."
        });
    }

    [HttpPost("deploy/{machineId}")]
    public async Task<ActionResult> DeployToMachine(string machineId)
    {
        try
        {
            var result = await mediator.Send(new UpdateAgentCommand(machineId));
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
