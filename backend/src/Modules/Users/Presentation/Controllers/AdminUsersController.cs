using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Users.Commands.SetUserRole;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Admin")]
public sealed class AdminUsersController : ControllerBase
{
    [HttpPost("{id:guid}/role")]
    public async Task<IActionResult> SetRole(
        Guid id,
        [FromBody] SetUserRoleBody body2,
        [FromServices] SetUserRoleHandler handler)
    {
        var result = await handler.HandleAsync(
            new SetUserRoleCommand(id, body2.Role));
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] Users.Application.Abstractions.IUserRepository repo)
    {
        var all = await repo.GetByIdAsync(Guid.Empty);
        return Ok(new { message = "Not implemented; use GET /api/users/{id}" });
    }
}

public sealed record SetUserRoleBody(int Role);