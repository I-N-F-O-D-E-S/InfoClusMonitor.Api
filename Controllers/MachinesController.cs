using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Machines;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachinesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Machine>>> GetAll()
    {
        var machines = await mediator.Send(new GetAllMachinesQuery());
        return Ok(machines);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Machine>> GetById(string id)
    {
        var machine = await mediator.Send(new GetMachineByIdQuery(id));
        if (machine is null)
            return NotFound();

        return Ok(machine);
    }

    [HttpPost("{id}/refresh")]
    public async Task<ActionResult<Machine>> Refresh(string id)
    {
        var machine = await mediator.Send(new RefreshMachineTelemetryCommand(id));
        if (machine is null)
            return NotFound();

        return Ok(machine);
    }

    [HttpPut("{id}/name")]
    public async Task<ActionResult<Machine>> UpdateName(string id, [FromBody] UpdateMachineNameDto dto)
    {
        var machine = await mediator.Send(new UpdateMachineNameCommand(id, dto.Name));
        if (machine is null)
            return NotFound();

        return Ok(machine);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var deleted = await mediator.Send(new DeleteMachineCommand(id));
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
