using MediatR;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.ScheduledTasks;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduledTasksController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ScheduledTaskDto>>> GetAll([FromQuery] string? machineId = null)
    {
        var result = await mediator.Send(new GetAllScheduledTasksQuery(machineId));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScheduledTaskDto>> GetById(string id)
    {
        var result = await mediator.Send(new GetScheduledTaskByIdQuery(id));
        if (result is null)
            return NotFound(new { error = "Tarea programada no encontrada." });

        return Ok(result);
    }

    [HttpGet("{id}/executions")]
    public async Task<ActionResult<List<ScheduledTaskExecutionDto>>> GetExecutions(string id, [FromQuery] int limit = 50)
    {
        var result = await mediator.Send(new GetScheduledTaskExecutionsQuery(id, limit));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ScheduledTaskDto>> Create([FromBody] CreateScheduledTaskDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.MachineId))
            return BadRequest(new { error = "El parámetro MachineId es obligatorio." });

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { error = "El nombre de la tarea es obligatorio." });

        if (string.IsNullOrWhiteSpace(dto.Command))
            return BadRequest(new { error = "El comando bash a programar es obligatorio." });

        try
        {
            var result = await mediator.Send(new CreateScheduledTaskCommand(dto));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ScheduledTaskDto>> Update(string id, [FromBody] UpdateScheduledTaskDto dto)
    {
        try
        {
            var result = await mediator.Send(new UpdateScheduledTaskCommand(id, dto));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id}/toggle")]
    public async Task<ActionResult<ScheduledTaskDto>> Toggle(string id)
    {
        try
        {
            var result = await mediator.Send(new ToggleScheduledTaskCommand(id));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/run")]
    public async Task<ActionResult> RunNow(string id)
    {
        try
        {
            await mediator.Send(new RunScheduledTaskNowCommand(id));
            return Ok(new { message = "Orden de ejecución manual enviada exitosamente al servidor." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        var success = await mediator.Send(new DeleteScheduledTaskCommand(id));
        if (!success)
            return NotFound(new { error = "Tarea programada no encontrada." });

        return NoContent();
    }
}
