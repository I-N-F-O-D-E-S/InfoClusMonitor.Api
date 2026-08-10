using System.Security.Claims;
using MediatR;
using Microsoft.EntityFrameworkCore;
using InfoClusMonitor.Api.Data;
using InfoClusMonitor.Api.Models.Dtos;
using InfoClusMonitor.Api.Models.Entities;
using InfoClusMonitor.Api.Services.Auth;

namespace InfoClusMonitor.Api.Features.Auth;

public record RegisterCommand(
    string Username,
    string Email,
    string Password,
    string Role = "Admin"
) : IRequest<AuthResponseDto>;

public class RegisterHandler(AppDbContext db, TokenService tokenService)
    : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var normalizedUsername = cmd.Username.Trim();
        var normalizedEmail = cmd.Email.Trim().ToLowerInvariant();

        var exists = await db.Users.AnyAsync(u =>
            u.Username.ToLower() == normalizedUsername.ToLower() ||
            u.Email.ToLower() == normalizedEmail, ct);

        if (exists)
        {
            throw new InvalidOperationException("El nombre de usuario o correo electrónico ya está registrado.");
        }

        var user = new User
        {
            UserId = Guid.NewGuid().ToString(),
            Username = normalizedUsername,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(cmd.Password),
            Role = string.IsNullOrWhiteSpace(cmd.Role) ? "Admin" : cmd.Role.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
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
