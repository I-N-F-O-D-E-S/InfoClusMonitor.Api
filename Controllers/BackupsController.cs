using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Backups;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BackupDto>>> GetAll([FromQuery] string? machineId = null)
    {
        var result = await mediator.Send(new GetAllBackupsQuery(machineId));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BackupDto>> GetById(string id)
    {
        var result = await mediator.Send(new GetBackupByIdQuery(id));
        if (result is null)
            return NotFound(new { error = "Copia de seguridad no encontrada." });

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<BackupDto>> Create([FromBody] CreateBackupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.MachineId))
            return BadRequest(new { error = "El parámetro MachineId es obligatorio." });

        if (string.IsNullOrWhiteSpace(dto.SourcePath))
            return BadRequest(new { error = "El parámetro SourcePath es obligatorio." });

        try
        {
            var result = await mediator.Send(new CreateBackupCommand(dto));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult<RestoreBackupResultDto>> Restore(string id, [FromBody] RestoreBackupDto dto)
    {
        try
        {
            var result = await mediator.Send(new RestoreBackupCommand(id, dto));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var success = await mediator.Send(new DeleteBackupCommand(id));
        if (!success)
            return NotFound(new { error = "Copia de seguridad no encontrada." });

        return NoContent();
    }
}
