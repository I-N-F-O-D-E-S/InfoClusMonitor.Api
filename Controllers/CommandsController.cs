using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Commands;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Command>>> GetAll(
        [FromQuery] string? machineId,
        [FromQuery] CommandStatus? status)
    {
        var commands = await mediator.Send(new GetAllCommandsQuery(machineId, status));
        return Ok(commands);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<Command>> GetById(long id)
    {
        var command = await mediator.Send(new GetCommandByIdQuery(id));
        if (command is null)
            return NotFound();

        return Ok(command);
    }

    [HttpPost]
    public async Task<ActionResult<Command>> Create(CreateCommandDto dto)
    {
        Command command;
        try
        {
            command = await mediator.Send(new CreateCommandCommand(dto.MachineId, dto.Parameters));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return CreatedAtAction(nameof(GetById), new { id = command.Id }, command);
    }
}
