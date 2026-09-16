using System.Security.Claims;
using AgentCore.Application.Abstractions;
using AgentCore.Infrastructure.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AgentCore.Presentation.Controllers;

[ApiController]
[Route("api/users/me/google-calendar")]
[Authorize]
public sealed class GoogleCalendarController : ControllerBase
{
    private readonly IGoogleOAuthService _oauthService;
    private readonly IUserGoogleTokenRepository _tokenRepository;
    private readonly IConfiguration _configuration;

    public GoogleCalendarController(
        IGoogleOAuthService oauthService,
        IUserGoogleTokenRepository tokenRepository,
        IConfiguration configuration)
    {
        _oauthService = oauthService;
        _tokenRepository = tokenRepository;
        _configuration = configuration;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var token = await _tokenRepository.GetByUserIdAsync(
            userId, cancellationToken);

        if (token is null)
        {
            return Ok(new
            {
                connected = false,
                googleEmail = (string?)null,
                scopes = (string?)null,
                accessTokenExpiresAt = (DateTime?)null
            });
        }

        return Ok(new
        {
            connected = true,
            googleEmail = token.GoogleEmail,
            scopes = token.Scopes,
            accessTokenExpiresAt = token.AccessTokenExpiresAt
        });
    }

    [HttpGet("connect")]
    public IActionResult Connect()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var url = _oauthService.BuildAuthorizationUrl(userId);
        return Ok(new { url });
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        var frontendBaseUrl = (_configuration["Frontend:BaseUrl"]
            ?? "http://localhost:3000").TrimEnd('/');

        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"{frontendBaseUrl}/profile?google=error&reason={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(new { error = "missing_code_or_state" });
        }

        if (!Guid.TryParse(state, out var userId))
        {
            return BadRequest(new { error = "invalid_state" });
        }

        var result = await _oauthService.ExchangeCodeAsync(
            userId,
            code,
            cancellationToken);

        if (!result.Success)
        {
            return Redirect(
                $"{frontendBaseUrl}/profile?google=error&reason={Uri.EscapeDataString(result.Error ?? "exchange_failed")}");
        }

        return Redirect($"{frontendBaseUrl}/profile?google=connected");
    }

    [HttpDelete("disconnect")]
    public async Task<IActionResult> Disconnect(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        await _tokenRepository.DeleteAsync(userId, cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out userId);
    }
}