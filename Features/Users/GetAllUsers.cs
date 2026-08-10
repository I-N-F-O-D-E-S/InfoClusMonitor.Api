using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Features.Users;

public record GetAllUsersQuery : IRequest<List<UserDto>>;

public class GetAllUsersHandler(AppDbContext db)
    : IRequestHandler<GetAllUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetAllUsersQuery query, CancellationToken ct)
    {
        return await db.Users
            .OrderBy(u => u.Username)
            .Select(u => new UserDto(u.Id, u.UserId, u.Username, u.Email, u.Role, u.CreatedAt, u.LastLoginAt))
            .ToListAsync(ct);
    }
}
