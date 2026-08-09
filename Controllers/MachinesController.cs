using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Hubs;
using InfoClusMonitor.Api.Models;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MachinesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<MachineHub> _hub;

    public MachinesController(AppDbContext db, IHubContext<MachineHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<Machine>>> GetAll()
    {
        var machines = await _db.Machines
            .OrderByDescending(m => m.LastHeartbeat)
            .ToListAsync();
        return Ok(machines);
    }

    [HttpGet("{agentId}")]
    public async Task<ActionResult<Machine>> GetById(string agentId)
    {
        var machine = await _db.Machines
            .Include(m => m.Commands.OrderByDescending(c => c.CreatedAt).Take(20))
            .FirstOrDefaultAsync(m => m.Id == agentId);

        if (machine is null)
            return NotFound();

        return Ok(machine);
    }

    [HttpDelete("{agentId}")]
    public async Task<ActionResult> Delete(string agentId)
    {
        var machine = await _db.Machines.FindAsync(agentId);
        if (machine is null)
            return NotFound();

        _db.Machines.Remove(machine);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group("all-machines")
            .SendAsync("MachineDeleted", agentId);

        return NoContent();
    }

    [HttpPost("{agentId}/heartbeat")]
    public async Task<ActionResult> Heartbeat(string agentId, AgentHeartbeatDto dto)
    {
        var machine = await _db.Machines.FindAsync(agentId);
        if (machine is null)
            return NotFound();

        machine.LastHeartbeat = DateTime.UtcNow;
        machine.Status = MachineStatus.Online;
        machine.AgentVersion = dto.AgentVersion;
        machine.Os = dto.Os;
        machine.IpAddress = dto.IpAddress;
        machine.CpuPercent = dto.CpuPercent;
        machine.MemoryPercent = dto.MemoryPercent;
        machine.DiskPercent = dto.DiskPercent;
        machine.Uptime = dto.Uptime;
        machine.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _hub.Clients.Group("all-machines")
            .SendAsync("MachineUpdated", machine);

        return Ok();
    }
}
