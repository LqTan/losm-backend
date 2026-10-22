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

    [AllowAnonymous]
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        [FromServices] Users.Application.Abstractions.IUserRepository repo = null!,
        CancellationToken cancellationToken = default)
    {
        var users = await repo.SearchUsersAsync(q, limit, cancellationToken);
        var result = users.Select(u => new
        {
            u.Id,
            u.Username,
            u.Email
        });
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("check-email")]
    public async Task<IActionResult> CheckEmail(
        [FromQuery] string email,
        [FromServices] Users.Application.Abstractions.IUserRepository repo)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { exists = false, message = "Email is required." });

        var user = await repo.GetByEmailAsync(email.Trim().ToLowerInvariant());
        return Ok(new
        {
            exists = user != null,
            email = email.Trim()
        });
    }

    [AllowAnonymous]
    [HttpPost("google-auth")]
    public async Task<IActionResult> GoogleAuth(
        [FromBody] GoogleAuthRequest request,
        [FromServices] Users.Application.Abstractions.IUserRepository repo,
        [FromServices] Users.Application.Abstractions.IPasswordHasher passwordHasher,
        [FromServices] Users.Application.Abstractions.ITokenProvider tokenProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await repo.GetByEmailAsync(email);

        if (user is null)
        {
            var username = !string.IsNullOrWhiteSpace(request.Name)
                ? request.Name.Trim()
                : email.Split('@')[0];

            var randomPassword = Guid.NewGuid().ToString("N");
            var hash = passwordHasher.Hash(randomPassword);

            user = new Users.Domain.Entities.User(
                Guid.NewGuid(),
                username,
                email,
                hash,
                Users.Domain.Entities.UserRole.User,
                request.AvatarUrl
            );
            await repo.AddAsync(user);
        }
        else
        {
            var updated = false;
            if (!string.IsNullOrWhiteSpace(request.Name) && (string.IsNullOrWhiteSpace(user.Username) || user.Username.StartsWith("google.user", StringComparison.OrdinalIgnoreCase)))
            {
                user.UpdateProfile(request.Name.Trim());
                updated = true;
            }
            if (!string.IsNullOrWhiteSpace(request.AvatarUrl) && user.AvatarUrl != request.AvatarUrl)
            {
                user.UpdateAvatar(request.AvatarUrl);
                updated = true;
            }
            if (updated)
            {
                await repo.UpdateAsync(user);
            }
        }

        var token = tokenProvider.Create(user);
        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            email = user.Email,
            role = user.Role.ToString(),
            avatar = request.AvatarUrl,
            token = token
        });
    }

    [AllowAnonymous]
    [HttpGet("google-login")]
    public IActionResult GoogleLogin(
        [FromQuery] string? redirect,
        [FromServices] Microsoft.Extensions.Configuration.IConfiguration config)
    {
        var clientId = config["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return BadRequest(new { message = "Google:ClientId is not configured in appsettings.json." });
        }

        var backendUrl = ResolveBackendUrl(config);
        var redirectUri = $"{backendUrl}/api/users/google/callback";
        var state = string.IsNullOrWhiteSpace(redirect) ? "/profile/calendar" : redirect;

        var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString("openid profile email")}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&prompt=select_account";

        return Redirect(authUrl);
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromServices] Microsoft.Extensions.Configuration.IConfiguration config,
        [FromServices] Users.Application.Abstractions.IUserRepository repo,
        [FromServices] Users.Application.Abstractions.IPasswordHasher passwordHasher,
        [FromServices] Users.Application.Abstractions.ITokenProvider tokenProvider,
        CancellationToken cancellationToken)
    {
        var frontendUrl = ResolveFrontendUrl(config);
        var redirectPath = string.IsNullOrWhiteSpace(state) ? "/profile/calendar" : state;

        if (!string.IsNullOrEmpty(error))
        {
            return Redirect($"{frontendUrl}/login?error={Uri.EscapeDataString(error)}");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Redirect($"{frontendUrl}/login?error=missing_code");
        }

        var clientId = config["Google:ClientId"];
        var clientSecret = config["Google:ClientSecret"];
        var backendUrl = ResolveBackendUrl(config);
        var redirectUri = $"{backendUrl}/api/users/google/callback";

        try
        {
            // 1. Đổi authorization code lấy access token từ Google
            using var httpClient = new HttpClient();
            var tokenParams = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId ?? "",
                ["client_secret"] = clientSecret ?? "",
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            };

            var tokenRes = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenParams),
                cancellationToken);

            if (!tokenRes.IsSuccessStatusCode)
            {
                var errBody = await tokenRes.Content.ReadAsStringAsync(cancellationToken);
                return Redirect($"{frontendUrl}/login?error=google_exchange_failed&details={Uri.EscapeDataString(errBody)}");
            }

            var tokenJson = await tokenRes.Content.ReadAsStringAsync(cancellationToken);
            using var tokenDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

            // 2. Lấy thông tin user (tên, email) từ Google API
            using var userinfoReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
            userinfoReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var userinfoRes = await httpClient.SendAsync(userinfoReq, cancellationToken);

            if (!userinfoRes.IsSuccessStatusCode)
            {
                return Redirect($"{frontendUrl}/login?error=google_userinfo_failed");
            }

            var userinfoJson = await userinfoRes.Content.ReadAsStringAsync(cancellationToken);
            using var userinfoDoc = System.Text.Json.JsonDocument.Parse(userinfoJson);

            var googleEmail = userinfoDoc.RootElement.TryGetProperty("email", out var eElem) ? eElem.GetString() : null;
            var googleName = userinfoDoc.RootElement.TryGetProperty("name", out var nElem) ? nElem.GetString() : null;
            var googlePicture = userinfoDoc.RootElement.TryGetProperty("picture", out var pElem) ? pElem.GetString() : null;

            if (string.IsNullOrWhiteSpace(googleEmail))
            {
                return Redirect($"{frontendUrl}/login?error=no_email_provided");
            }

            var email = googleEmail.Trim().ToLowerInvariant();
            var user = await repo.GetByEmailAsync(email);

            if (user is null)
            {
                var username = !string.IsNullOrWhiteSpace(googleName)
                    ? googleName.Trim()
                    : email.Split('@')[0];

                var randomPassword = Guid.NewGuid().ToString("N");
                var hash = passwordHasher.Hash(randomPassword);

                user = new Users.Domain.Entities.User(
                    Guid.NewGuid(),
                    username,
                    email,
                    hash,
                    Users.Domain.Entities.UserRole.User,
                    googlePicture
                );
                await repo.AddAsync(user);
            }
            else
            {
                var updated = false;
                if (!string.IsNullOrWhiteSpace(googleName) && (string.IsNullOrWhiteSpace(user.Username) || user.Username.StartsWith("google.user", StringComparison.OrdinalIgnoreCase)))
                {
                    user.UpdateProfile(googleName.Trim());
                    updated = true;
                }
                if (!string.IsNullOrWhiteSpace(googlePicture) && user.AvatarUrl != googlePicture)
                {
                    user.UpdateAvatar(googlePicture);
                    updated = true;
                }
                if (updated)
                {
                    await repo.UpdateAsync(user);
                }
            }

            // 3. Cấp JWT token và chuyển hướng về frontend callback
            var token = tokenProvider.Create(user);

            var callbackUrl = $"{frontendUrl}/auth/callback" +
                $"?token={Uri.EscapeDataString(token)}" +
                $"&username={Uri.EscapeDataString(user.Username)}" +
                $"&email={Uri.EscapeDataString(user.Email)}" +
                $"&role={Uri.EscapeDataString(user.Role.ToString())}" +
                (string.IsNullOrWhiteSpace(googlePicture) ? "" : $"&avatar={Uri.EscapeDataString(googlePicture)}") +
                $"&redirect={Uri.EscapeDataString(redirectPath)}";

            return Redirect(callbackUrl);
        }
        catch (Exception ex)
        {
            return Redirect($"{frontendUrl}/login?error=oauth_error&details={Uri.EscapeDataString(ex.Message)}");
        }
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

    private string ResolveBackendUrl(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        var configured = config["Backend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        if (!string.IsNullOrWhiteSpace(host))
        {
            return $"{scheme}://{host}".TrimEnd('/');
        }

        return "http://localhost:5232";
    }

    private string ResolveFrontendUrl(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        var configured = config["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        var origin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin) && !IsExternalOAuthHost(origin))
        {
            return origin.TrimEnd('/');
        }

        var referer = Request.Headers.Referer.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refUri) && !IsExternalOAuthHost(refUri.Host))
        {
            return $"{refUri.Scheme}://{refUri.Authority}".TrimEnd('/');
        }

        var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
        var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
        if (!string.IsNullOrWhiteSpace(host))
        {
            if (!host.Contains(":5232") && !host.Contains(":8082") && !host.Contains(":8080"))
            {
                return $"{scheme}://{host}".TrimEnd('/');
            }

            var hostWithoutPort = host.Split(':')[0];
            return $"{scheme}://{hostWithoutPort}:3000".TrimEnd('/');
        }

        return "http://localhost:3000";
    }

    private static bool IsExternalOAuthHost(string hostOrUrl)
    {
        return hostOrUrl.Contains("google.com", StringComparison.OrdinalIgnoreCase)
            || hostOrUrl.Contains("googleapis.com", StringComparison.OrdinalIgnoreCase)
            || hostOrUrl.Contains("googleusercontent.com", StringComparison.OrdinalIgnoreCase)
            || hostOrUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record GoogleAuthRequest(
    string Email,
    string? Name,
    string? GoogleId,
    string? AvatarUrl
);