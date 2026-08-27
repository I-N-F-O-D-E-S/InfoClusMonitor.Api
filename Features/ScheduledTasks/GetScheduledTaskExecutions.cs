using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record GetScheduledTaskExecutionsQuery(string TaskId, int Limit = 50) : IRequest<List<ScheduledTaskExecutionDto>>;

public class GetScheduledTaskExecutionsHandler(AppDbContext db) : IRequestHandler<GetScheduledTaskExecutionsQuery, List<ScheduledTaskExecutionDto>>
{
    public async Task<List<ScheduledTaskExecutionDto>> Handle(GetScheduledTaskExecutionsQuery request, CancellationToken ct)
    {
        long.TryParse(request.TaskId, out var numericId);

        var query = db.ScheduledTaskExecutions.AsNoTracking()
            .Where(e => e.TaskId == request.TaskId || (numericId > 0 && e.Id == numericId))
            .OrderByDescending(e => e.StartedAt)
            .Take(Math.Clamp(request.Limit, 1, 200));

        var executions = await query.ToListAsync(ct);

        return executions.Select(e => new ScheduledTaskExecutionDto(
            e.Id,
            e.ExecutionId,
            e.TaskId,
            e.MachineId,
            e.Hostname,
            e.TaskName,
            e.Command,
            e.Status,
            e.Result,
            e.ErrorMessage,
            e.ExitCode,
            e.DurationMs,
            e.StartedAt,
            e.CompletedAt,
            ScheduleCalculationHelper.FormatParaguayDateTime(e.StartedAt),
            FormatDuration(e.DurationMs)
        )).ToList();
    }

    private static string FormatDuration(long ms)
    {
        if (ms <= 0) return "0s";
        if (ms < 1000) return $"{ms}ms";
        var sec = ms / 1000.0;
        if (sec < 60) return $"{sec:0.#}s";
        var min = (int)(sec / 60);
        var remSec = (int)(sec % 60);
        return $"{min}m {remSec}s";
    }
}
