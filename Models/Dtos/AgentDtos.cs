namespace InfoClusMonitor.Api.Models.Dtos;

public record AgentRegisterDto(
    string AgentId,
    string Hostname,
    string Os,
    string IpAddress,
    string? PrivateIpAddress,
    string? PublicIpAddress,
    string AgentVersion
);

public record AgentHeartbeatDto(
    string? AgentId,
    string AgentVersion,
    string Os,
    string IpAddress,
    string? PrivateIpAddress,
    string? PublicIpAddress,
    double CpuPercent,
    double MemoryPercent,
    double DiskPercent,
    long Uptime
);
