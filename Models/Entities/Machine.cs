using InfoClusMonitor.Api.Commons;

namespace InfoClusMonitor.Api.Models.Entities;

public enum MachineStatus
{
    Online,
    Offline,
    Maintenance,
    Error
}

public class Machine : BaseEntity
{
    public string ExternalMachineId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string PrivateIpAddress { get; set; } = string.Empty;
    public string PublicIpAddress { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public MachineStatus Status { get; set; } = MachineStatus.Offline;
    public string AgentVersion { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryPercent { get; set; }
    public double DiskPercent { get; set; }
    public long Uptime { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Command> Commands { get; set; } = new();
}
