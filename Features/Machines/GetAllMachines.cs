using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Features.Machines;

public record GetAllMachinesQuery : IRequest<List<Machine>>;

public class GetAllMachinesHandler(AppDbContext db) : IRequestHandler<GetAllMachinesQuery, List<Machine>>
{
    public async Task<List<Machine>> Handle(GetAllMachinesQuery _, CancellationToken ct)
    {
        return await db.Machines
            .OrderByDescending(m => m.LastHeartbeat)
            .ToListAsync(ct);
    }
}
