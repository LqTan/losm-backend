using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Users.Commands.LoginUser;
using Users.Application.Users.Commands.RegisterUser;
using Users.Application.Users.Commands.UpdateProfile;
using Users.Application.Users.Queries.GetUserById;
using Users.Presentation.Contracts;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetUserByIdHandler handler
    )
    {
        var user = await handler.HandleAsync(
            new GetUserByIdQuery(id)
        );
        if (user is null)
            return NotFound();
        return Ok(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(
        [FromServices] GetUserByIdHandler handler
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var user = await handler.HandleAsync(
            new GetUserByIdQuery(userId)
        );
        if (user is null)
            return NotFound();
        return Ok(user);
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(
        UpdateProfileRequest request,
        [FromServices] UpdateProfileHandler handler
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var result = await handler.HandleAsync(
            new UpdateProfileCommand(
                userId,
                request.Username
            )
        );
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterUserCommand command,
        [FromServices] RegisterUserHandler handler
    )
    {
        var result = await handler.HandleAsync(command);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            result
        );
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginUserCommand command,
        [FromServices] LoginUserHandler handler
    )
    {
        var result = await handler.HandleAsync(command);
        return Ok(result);
    }
}