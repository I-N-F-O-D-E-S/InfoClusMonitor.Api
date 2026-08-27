using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services;

namespace InfoClusMonitor.Api.Features.ScheduledTasks;

public record GetScheduledTaskByIdQuery(string TaskId) : IRequest<ScheduledTaskDto?>;

public class GetScheduledTaskByIdHandler(AppDbContext db) : IRequestHandler<GetScheduledTaskByIdQuery, ScheduledTaskDto?>
{
    public async Task<ScheduledTaskDto?> Handle(GetScheduledTaskByIdQuery request, CancellationToken ct)
    {
        long.TryParse(request.TaskId, out var numericId);

        var t = await db.ScheduledTasks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TaskId == request.TaskId || (numericId > 0 && x.Id == numericId), ct);

        if (t is null) return null;

        return new ScheduledTaskDto(
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
        );
    }
}
