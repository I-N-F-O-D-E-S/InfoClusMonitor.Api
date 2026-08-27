using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record GetAllScheduledTasksQuery(string? MachineId = null) : IRequest<List<ScheduledTaskDto>>;

public class GetAllScheduledTasksHandler(AppDbContext db) : IRequestHandler<GetAllScheduledTasksQuery, List<ScheduledTaskDto>>
{
    public async Task<List<ScheduledTaskDto>> Handle(GetAllScheduledTasksQuery request, CancellationToken ct)
    {
        var query = db.ScheduledTasks.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.MachineId))
        {
            query = query.Where(t => t.MachineId == request.MachineId);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        return tasks.Select(t => new ScheduledTaskDto(
            t.Id,
            t.TaskId,
            t.MachineId,
            t.Hostname,
            t.Name,
            t.Description,
            t.Command,
            t.ScheduleType,
            t.IntervalValue,
            t.ScheduledTime,
            t.DaysOfWeek,
            t.SpecificDate,
            t.CronExpression,
            t.Timezone,
            t.IsEnabled,
            t.NextRunAt,
            t.LastRunAt,
            t.LastStatus,
            t.LastResult,
            t.LastDurationMs,
            t.CreatedAt,
            t.UpdatedAt,
            ScheduleCalculationHelper.GenerateScheduleSummary(t),
            ScheduleCalculationHelper.FormatParaguayDateTime(t.NextRunAt),
            ScheduleCalculationHelper.FormatParaguayDateTime(t.LastRunAt)
        )).ToList();
    }
}
