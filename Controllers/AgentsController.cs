using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Hubs;
using InfoClusMonitor.Api.Models;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/agents")]
public class AgentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<MachineHub> _hub;

    public AgentsController(AppDbContext db, IHubContext<MachineHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    [HttpPost("register")]
    public async Task<ActionResult<Machine>> Register(AgentRegisterDto dto)
    {
        var existing = await _db.Machines.FindAsync(dto.AgentId);

        if (existing is not null)
        {
            existing.Hostname = dto.Hostname;
            existing.IpAddress = dto.IpAddress;
            existing.Os = dto.Os;
            existing.AgentVersion = dto.AgentVersion;
            existing.LastHeartbeat = DateTime.UtcNow;
            existing.Status = MachineStatus.Online;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            await _hub.Clients.Group("all-machines")
                .SendAsync("MachineUpdated", existing);

            return Ok(existing);
        }

        var machine = new Machine
        {
            Id = dto.AgentId,
            Name = dto.Hostname,
            Hostname = dto.Hostname,
            IpAddress = dto.IpAddress,
            Os = dto.Os,
            AgentVersion = dto.AgentVersion,
            Status = MachineStatus.Online
        };

        _db.Machines.Add(machine);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group("all-machines")
            .SendAsync("MachineCreated", machine);

        return CreatedAtAction(nameof(MachinesController.GetById), "Machines", new { agentId = machine.Id }, machine);
    }
}
