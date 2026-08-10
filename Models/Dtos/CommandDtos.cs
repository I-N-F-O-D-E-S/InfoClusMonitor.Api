namespace InfoClusMonitor.Api.Models.Dtos;

public record CreateCommandDto(
    string MachineId,
    string Parameters
);

public record CommandResultDto(
    string CommandId,
    string Status,
    string Result
);
