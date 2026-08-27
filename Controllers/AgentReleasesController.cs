using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Agents;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentReleasesController(IMediator mediator, IMinioService minio) : ControllerBase
{
    private string GenerateInstallCommand()
    {
        var publicUrl = minio.GetPublicUrl(UpdateAgentHandler.PackageObjectName, minio.ReleasesBucketName);
        return $"mkdir -p /tmp/infoclus_agent_pkg && cd /tmp/infoclus_agent_pkg && curl -fsSL '{publicUrl}' -o package.tar.gz && tar -xzf package.tar.gz && sed -i 's/\\r$//' install.sh 2>/dev/null || true && chmod +x install.sh agent.py && sudo bash install.sh";
    }

    [HttpGet("install-command")]
    public async Task<ActionResult> GetInstallCommand()
    {
        var packageExists = await minio.ObjectExistsAsync(UpdateAgentHandler.PackageObjectName, minio.ReleasesBucketName);
        var command = GenerateInstallCommand();
        var downloadUrl = minio.GetPublicUrl(UpdateAgentHandler.PackageObjectName, minio.ReleasesBucketName);

        return Ok(new
        {
            packageAvailable = packageExists,
            installCommand = command,
            downloadUrl = downloadUrl,
            targetVersion = "1.2.0"
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

        var installCommand = GenerateInstallCommand();
        var downloadUrl = minio.GetPublicUrl(UpdateAgentHandler.PackageObjectName, minio.ReleasesBucketName);

        return Ok(new
        {
            message = "Paquete (agent.py + install.sh) subido y empaquetado exitosamente en MinIO.",
            installCommand = installCommand,
            downloadUrl = downloadUrl,
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
