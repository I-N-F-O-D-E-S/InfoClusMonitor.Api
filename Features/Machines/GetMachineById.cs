using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Features.Machines;

public record GetMachineByIdQuery(string Identifier) : IRequest<Machine?>;

public class GetMachineByIdHandler(AppDbContext db) : IRequestHandler<GetMachineByIdQuery, Machine?>
{
    public async Task<Machine?> Handle(GetMachineByIdQuery query, CancellationToken ct)
    {
        long.TryParse(query.Identifier, out var numericId);

        return await db.Machines
            .Include(m => m.Commands.OrderByDescending(c => c.CreatedAt).Take(20))
            .FirstOrDefaultAsync(m => m.ExternalMachineId == query.Identifier || (numericId > 0 && m.Id == numericId), ct);
    }
}
