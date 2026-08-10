using MediatR;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Features.Commands;

public record GetCommandByIdQuery(long Id) : IRequest<Command?>;

public class GetCommandByIdHandler(AppDbContext db) : IRequestHandler<GetCommandByIdQuery, Command?>
{
    public async Task<Command?> Handle(GetCommandByIdQuery query, CancellationToken ct)
    {
        return await db.Commands.FindAsync([query.Id], ct);
    }
}
