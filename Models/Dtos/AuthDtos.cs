namespace InfoClusMonitor.Api.Models.Dtos;

public record RegisterUserDto(
    string Username,
    string Email,
    string Password,
    string? Role = "Admin"
);

public record LoginDto(
    string UsernameOrEmail,
    string Password
);

public record UserDto(
    long Id,
    string UserId,
    string Username,
    string Email,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record AuthResponseDto(
    string Token,
    DateTime Expiration,
    UserDto User
);
