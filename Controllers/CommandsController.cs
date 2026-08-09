using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Hubs;
using InfoClusMonitor.Api.Models;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommandsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<MachineHub> _hub;
    private readonly IRabbitMQService _rabbit;

    public CommandsController(AppDbContext db, IHubContext<MachineHub> hub, IRabbitMQService rabbit)
    {
        _db = db;
        _hub = hub;
        _rabbit = rabbit;
    }

    [HttpGet]
    public async Task<ActionResult<List<Command>>> GetAll(
        [FromQuery] string? machineId,
        [FromQuery] CommandStatus? status)
    {
        var query = _db.Commands.AsQueryable();

        if (!string.IsNullOrEmpty(machineId))
            query = query.Where(c => c.MachineId == machineId);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        var commands = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Ok(commands);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Command>> GetById(Guid id)
    {
        var command = await _db.Commands.FindAsync(id);
        if (command is null)
            return NotFound();

        return Ok(command);
    }

    [HttpPost]
    public async Task<ActionResult<Command>> Create(CreateCommandDto dto)
    {
        var machine = await _db.Machines.FindAsync(dto.MachineId);
        if (machine is null)
            return BadRequest("Machine not found");

        if (machine.Status != MachineStatus.Online)
            return BadRequest("Machine is not online");

        var command = new Command
        {
            MachineId = dto.MachineId,
            Type = "Exe",
            Parameters = dto.Parameters,
            Status = CommandStatus.Pending
        };

        _db.Commands.Add(command);
        await _db.SaveChangesAsync();

        await _rabbit.SendCommandAsync(command.Id, dto.MachineId, dto.Parameters);

        command.Status = CommandStatus.Sent;
        command.ExecutedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hub.Clients.Group($"machine-{dto.MachineId}")
            .SendAsync("CommandCreated", command);
        await _hub.Clients.Group("all-machines")
            .SendAsync("CommandCreated", command);

        return CreatedAtAction(nameof(GetById), new { id = command.Id }, command);
    }
}
