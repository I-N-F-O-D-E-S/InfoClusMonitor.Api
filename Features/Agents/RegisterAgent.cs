using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Agents;

public record RegisterAgentCommand(
    string AgentId,
    string Hostname,
    string Os,
    string IpAddress,
    string? PrivateIpAddress,
    string? PublicIpAddress,
    string AgentVersion
) : IRequest<Machine>;

public class RegisterAgentHandler(AppDbContext db, IMediator mediator)
    : IRequestHandler<RegisterAgentCommand, Machine>
{
    public async Task<Machine> Handle(RegisterAgentCommand cmd, CancellationToken ct)
    {
        var existing = await db.Machines.FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.AgentId, ct);

        var privateIp = !string.IsNullOrWhiteSpace(cmd.PrivateIpAddress) ? cmd.PrivateIpAddress : cmd.IpAddress;
        var publicIp = !string.IsNullOrWhiteSpace(cmd.PublicIpAddress) ? cmd.PublicIpAddress : cmd.IpAddress;

        if (existing is not null)
        {
            existing.Hostname = cmd.Hostname;
            existing.IpAddress = cmd.IpAddress;
            existing.PrivateIpAddress = privateIp;
            existing.PublicIpAddress = publicIp;
            existing.Os = cmd.Os;
            existing.AgentVersion = cmd.AgentVersion;
            existing.LastHeartbeat = DateTime.UtcNow;
            existing.Status = MachineStatus.Online;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.IsDeleted = false;

            await db.SaveChangesAsync(ct);
            await mediator.Publish(new MachineUpdatedNotification(existing), ct);
            return existing;
        }

        var machine = new Machine
        {
            ExternalMachineId = cmd.AgentId,
            Name = cmd.Hostname,
            Hostname = cmd.Hostname,
            IpAddress = cmd.IpAddress,
            PrivateIpAddress = privateIp,
            PublicIpAddress = publicIp,
            Os = cmd.Os,
            AgentVersion = cmd.AgentVersion,
            Status = MachineStatus.Online,
            LastHeartbeat = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Machines.Add(machine);
        await db.SaveChangesAsync(ct);
        await mediator.Publish(new MachineCreatedNotification(machine), ct);
        return machine;
    }
}
