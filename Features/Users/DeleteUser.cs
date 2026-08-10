using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;

namespace InfoClusMonitor.Api.Features.Users;

public record DeleteUserCommand(string Identifier) : IRequest<bool>;

public class DeleteUserHandler(AppDbContext db)
    : IRequestHandler<DeleteUserCommand, bool>
{
    public async Task<bool> Handle(DeleteUserCommand cmd, CancellationToken ct)
    {
        long.TryParse(cmd.Identifier, out var numericId);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.UserId == cmd.Identifier || (numericId > 0 && u.Id == numericId), ct);

        if (user is null) return false;

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
