namespace InfoClusMonitor.Api.Models.Dtos;

public record CreateScheduledTaskDto(
    string MachineId,
    string Name,
    string? Description,
    string Command,
    string ScheduleType,
    int? IntervalValue,
    string? ScheduledTime,
    string? DaysOfWeek,
    DateTime? SpecificDate,
    string? CronExpression,
    string? Timezone
);

public record UpdateScheduledTaskDto(
    string Name,
    string? Description,
    string Command,
    string ScheduleType,
    int? IntervalValue,
    string? ScheduledTime,
    string? DaysOfWeek,
    DateTime? SpecificDate,
    string? CronExpression,
    string? Timezone
);

public record ScheduledTaskDto(
    long Id,
    string TaskId,
    string MachineId,
    string Hostname,
    string Name,
    string Description,
    string Command,
    string ScheduleType,
    int? IntervalValue,
    string? ScheduledTime,
    string? DaysOfWeek,
    DateTime? SpecificDate,
    string? CronExpression,
    string Timezone,
    bool IsEnabled,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastStatus,
    string? LastResult,
    long? LastDurationMs,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ScheduleSummary,
    string NextRunParaguayFormatted,
    string LastRunParaguayFormatted
);

public record ScheduledTaskExecutionDto(
    long Id,
    string ExecutionId,
    string TaskId,
    string MachineId,
    string Hostname,
    string TaskName,
    string Command,
    string Status,
    string? Result,
    string? ErrorMessage,
    int? ExitCode,
    long DurationMs,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string StartedAtParaguayFormatted,
    string DurationFormatted
);
