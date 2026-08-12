using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Files;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilesController(IMediator mediator) : ControllerBase
{
    [HttpGet("browse")]
    public async Task<ActionResult<DirectoryContentDto>> Browse([FromQuery] string machineId, [FromQuery] string? path = "/")
    {
        if (string.IsNullOrWhiteSpace(machineId))
            return BadRequest("El parámetro machineId es obligatorio.");

        var result = await mediator.Send(new BrowseDirectoryQuery(machineId, path ?? "/"));
        return Ok(result);
    }

    [HttpPost("browse")]
    public async Task<ActionResult<DirectoryContentDto>> BrowsePost([FromBody] BrowseDirectoryRequestDto req)
    {
        if (string.IsNullOrWhiteSpace(req.MachineId))
            return BadRequest("El parámetro MachineId es obligatorio.");

        var result = await mediator.Send(new BrowseDirectoryQuery(req.MachineId, req.Path ?? "/"));
        return Ok(result);
    }
}
