using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Commons.Enums;

namespace InfoClusMonitor.Api.Features.Commands;

public record GetAllCommandsQuery(
    string? MachineId = null,
    CommandStatus? Status = null
) : IRequest<List<Command>>;

public class GetAllCommandsHandler(AppDbContext db) : IRequestHandler<GetAllCommandsQuery, List<Command>>
{
    public async Task<List<Command>> Handle(GetAllCommandsQuery query, CancellationToken ct)
    {
        var q = db.Commands.AsQueryable();

        if (!string.IsNullOrEmpty(query.MachineId))
        {
            long.TryParse(query.MachineId, out var numericId);
            q = q.Where(c => c.ExternalMachineId == query.MachineId || (numericId > 0 && c.Id == numericId));
        }

        if (query.Status.HasValue)
            q = q.Where(c => c.Status == query.Status.Value);

        return await q
            .OrderByDescending(c => c.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
    }
}
