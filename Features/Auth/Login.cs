using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Services.Auth;

namespace InfoClusMonitor.Api.Features.Auth;

public record LoginCommand(
    string UsernameOrEmail,
    string Password
) : IRequest<AuthResponseDto>;

public class LoginHandler(AppDbContext db, TokenService tokenService)
    : IRequestHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var input = cmd.UsernameOrEmail.Trim();
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.Username.ToLower() == input.ToLower() ||
            u.Email.ToLower() == input.ToLower(), ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(cmd.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Credenciales inválidas.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var expirationDate = DateTime.UtcNow.AddDays(7);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var token = await tokenService.GenerateJwtToken(claims, expirationDate);
        var userDto = new UserDto(user.Id, user.UserId, user.Username, user.Email, user.Role, user.CreatedAt, user.LastLoginAt);

        return new AuthResponseDto(token, expirationDate, userDto);
    }
}
