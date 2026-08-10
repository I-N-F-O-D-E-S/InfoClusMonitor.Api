using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfoClusMonitor.Api.Features.Users;
using InfoClusMonitor.Api.Models.Dtos;

namespace InfoClusMonitor.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await mediator.Send(new GetAllUsersQuery());
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        var user = await mediator.Send(new GetUserByIdQuery(id));
        if (user is null) return NotFound();
        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await mediator.Send(new DeleteUserCommand(id));
        if (!deleted) return NotFound();
        return NoContent();
    }
}
