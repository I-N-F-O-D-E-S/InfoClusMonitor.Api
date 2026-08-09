namespace InfoClusMonitor.Api.Models;

public record AgentRegisterDto(
    string AgentId,
    string Hostname,
    string Os,
    string IpAddress,
    string AgentVersion
);

public record AgentHeartbeatDto(
    string AgentVersion,
    string Os,
    string IpAddress,
    double CpuPercent,
    double MemoryPercent,
    double DiskPercent,
    long Uptime
);

public record CreateCommandDto(
    string MachineId,
    string Parameters
);

public record CommandResultDto(
    string CommandId,
    string Status,
    string Result
);
