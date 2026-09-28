using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Users.Application.Users.Admin.Commands.AdminLogin;
using Users.Application.Users.Admin.Commands.VerifyCurrentPassword;
using Users.Application.Users.Admin.Queries.GetAdminProfile;

namespace Users.Presentation.Controllers;

[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AdminLoginResult>> Login(
        [FromBody] AdminLoginRequest request,
        [FromServices] AdminLoginHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new AdminLoginCommand(request.Email, request.Password),
            cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { success = true });

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("me")]
    public async Task<ActionResult> Me(
        [FromServices] GetAdminProfileHandler handler,
        CancellationToken cancellationToken)
    {
        var profile = await handler.HandleAsync(
            User,
            cancellationToken);
        return Ok(profile);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("verify-password")]
    public async Task<ActionResult<VerifyCurrentPasswordResult>> VerifyPassword(
        [FromBody] VerifyCurrentPasswordRequest request,
        [FromServices] VerifyCurrentPasswordHandler handler,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var isValid = await handler.HandleAsync(
            new VerifyCurrentPasswordCommand(userId, request.Password),
            cancellationToken);

        return Ok(new VerifyCurrentPasswordResult(isValid));
    }
}

public sealed record AdminLoginRequest(
    string Email,
    string Password);

public sealed record VerifyCurrentPasswordRequest(string Password);

public sealed record VerifyCurrentPasswordResult(bool IsValid);
