using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Auth;
using InfoClusMonitor.Api.Features.Users;
using InfoClusMonitor.Api.Models;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterUserDto dto)
    {
        try
        {
            var result = await mediator.Send(new RegisterCommand(
                dto.Username,
                dto.Email,
                dto.Password,
                dto.Role ?? "Admin"
            ));

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
    {
        try
        {
            var result = await mediator.Send(new LoginCommand(
                dto.UsernameOrEmail,
                dto.Password
            ));

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await mediator.Send(new GetUserByIdQuery(userId));
        if (user is null)
        {
            return NotFound();
        }

        return Ok(user);
    }
}
