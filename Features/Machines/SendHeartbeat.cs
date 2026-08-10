using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Features.Notifications;

namespace InfoClusMonitor.Api.Features.Machines;

public record SendHeartbeatCommand(
    string AgentId,
    string AgentVersion,
    string Os,
    string IpAddress,
    string? PrivateIpAddress,
    string? PublicIpAddress,
    double CpuPercent,
    double MemoryPercent,
    double DiskPercent,
    long Uptime
) : IRequest<Machine?>;

public class SendHeartbeatHandler(AppDbContext db, IMediator mediator)
    : IRequestHandler<SendHeartbeatCommand, Machine?>
{
    public async Task<Machine?> Handle(SendHeartbeatCommand cmd, CancellationToken ct)
    {
        var machine = await db.Machines.FirstOrDefaultAsync(m => m.ExternalMachineId == cmd.AgentId, ct);
        var privateIp = !string.IsNullOrWhiteSpace(cmd.PrivateIpAddress) ? cmd.PrivateIpAddress : cmd.IpAddress;
        var publicIp = !string.IsNullOrWhiteSpace(cmd.PublicIpAddress) ? cmd.PublicIpAddress : cmd.IpAddress;

        if (machine is null)
        {
            machine = new Machine
            {
                ExternalMachineId = cmd.AgentId,
                Name = cmd.AgentId,
                Hostname = cmd.AgentId,
                IpAddress = cmd.IpAddress,
                PrivateIpAddress = privateIp,
                PublicIpAddress = publicIp,
                Os = cmd.Os,
                AgentVersion = cmd.AgentVersion,
                Status = MachineStatus.Online,
                CpuPercent = cmd.CpuPercent,
                MemoryPercent = cmd.MemoryPercent,
                DiskPercent = cmd.DiskPercent,
                Uptime = cmd.Uptime,
                LastHeartbeat = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Machines.Add(machine);
            await db.SaveChangesAsync(ct);
            await mediator.Publish(new MachineCreatedNotification(machine), ct);
            return machine;
        }

        machine.LastHeartbeat = DateTime.UtcNow;
        machine.Status = MachineStatus.Online;
        machine.AgentVersion = cmd.AgentVersion;
        machine.Os = cmd.Os;
        machine.IpAddress = cmd.IpAddress;
        if (!string.IsNullOrWhiteSpace(cmd.PrivateIpAddress)) machine.PrivateIpAddress = cmd.PrivateIpAddress;
        if (!string.IsNullOrWhiteSpace(cmd.PublicIpAddress)) machine.PublicIpAddress = cmd.PublicIpAddress;
        machine.CpuPercent = cmd.CpuPercent;
        machine.MemoryPercent = cmd.MemoryPercent;
        machine.DiskPercent = cmd.DiskPercent;
        machine.Uptime = cmd.Uptime;
        machine.UpdatedAt = DateTime.UtcNow;
        machine.IsDeleted = false;

        await db.SaveChangesAsync(ct);
        await mediator.Publish(new MachineUpdatedNotification(machine), ct);
        return machine;
    }
}
