using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Features.Users;

public record GetUserByIdQuery(string Identifier) : IRequest<UserDto?>;

public class GetUserByIdHandler(AppDbContext db)
    : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        long.TryParse(query.Identifier, out var numericId);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.UserId == query.Identifier || (numericId > 0 && u.Id == numericId), ct);

        if (user is null) return null;

        return new UserDto(user.Id, user.UserId, user.Username, user.Email, user.Role, user.CreatedAt, user.LastLoginAt);
    }
}
