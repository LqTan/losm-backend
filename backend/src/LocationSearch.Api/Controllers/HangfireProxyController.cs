using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LocationSearch.Api.Controllers;

[ApiController]
[Route("api/admin/hangfire-proxy/{*path}")]
public sealed class HangfireProxyController : ControllerBase
{
    private const string AuthCookieName = "LosmAuth";
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "Proxy-Connection",
        "TE",
        "Trailer",
        "Trailers",
        "Transfer-Encoding",
        "Upgrade"
    };

    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly JwtBearerOptions _jwtOptions;

    public HangfireProxyController(
        IHttpClientFactory factory,
        IConfiguration config,
        IOptionsMonitor<JwtBearerOptions> jwtOptions)
    {
        _factory = factory;
        _config = config;
        _jwtOptions = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme);
    }

    [AcceptVerbs("GET", "POST", "DELETE", "PUT", "HEAD")]
    public async Task<IActionResult> Proxy(string? path = null)
    {
        var token = ReadToken();
        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized(new { message = "Missing token (query ?access_token=... or Authorization header or LosmAuth/losm_token cookie)." });
        }

        var valid = ValidateToken(token);
        if (valid is null || !valid.IsAdmin)
        {
            return Unauthorized(new { message = "Admin role required." });
        }

        Response.Cookies.Append(AuthCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        var innerPath = (path ?? "").Trim('/');
        var innerUrl = BuildInternalUrl(innerPath, Request.QueryString.Value);

        var client = _factory.CreateClient("hangfire-internal");
        var innerRequest = new HttpRequestMessage(new HttpMethod(Request.Method), innerUrl);
        innerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        innerRequest.Headers.Add("X-Internal-Proxy", "1");

        if (Request.ContentLength > 0)
        {
            Request.EnableBuffering();
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            innerRequest.Content = new ByteArrayContent(ms.ToArray());
            if (!string.IsNullOrEmpty(Request.ContentType))
            {
                innerRequest.Content.Headers.ContentType =
                    MediaTypeHeaderValue.Parse(Request.ContentType);
            }
        }

        HttpResponseMessage inner;
        try
        {
            inner = await client.SendAsync(innerRequest, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(502, new { message = $"Upstream Hangfire unreachable: {ex.Message}" });
        }

        var bytes = await inner.Content.ReadAsByteArrayAsync();
        var mediaType = inner.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";

        if (mediaType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            var html = Encoding.UTF8.GetString(bytes);
            var baseHref = $"/api/admin/hangfire-proxy/?access_token={Uri.EscapeDataString(token)}";
            var baseTag = $"<base href=\"{baseHref}\">";
            if (html.Contains("<head>", StringComparison.OrdinalIgnoreCase))
            {
                var headIndex = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                html = html.Insert(headIndex + "<head>".Length, baseTag);
            }
            else
            {
                html = baseTag + html;
            }

            // Rewrite root-relative /hangfire/ links and assets to /api/admin/hangfire-proxy/
            html = html.Replace("\"/hangfire/", "\"/api/admin/hangfire-proxy/");
            html = html.Replace("'/hangfire/", "'/api/admin/hangfire-proxy/");
            html = html.Replace("\"/hangfire\"", "\"/api/admin/hangfire-proxy\"");
            html = html.Replace("'/hangfire'", "'/api/admin/hangfire-proxy'");

            bytes = Encoding.UTF8.GetBytes(html);
            mediaType = "text/html; charset=utf-8";
        }

        Response.StatusCode = (int)inner.StatusCode;

        foreach (var h in inner.Headers)
        {
            if (h.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                continue;
            if (HopByHopHeaders.Contains(h.Key))
                continue;

            Response.Headers[h.Key] = h.Value.ToArray();
        }

        // CRITICAL: Prevent sending both Transfer-Encoding and Content-Length which crashes Node.js proxy parser
        Response.Headers.Remove("Transfer-Encoding");

        if (inner.Headers.Location is not null)
        {
            var loc = inner.Headers.Location.ToString();
            if (loc.StartsWith("/hangfire", StringComparison.OrdinalIgnoreCase))
            {
                var sub = loc.Substring("/hangfire".Length).TrimStart('/');
                var prefix = string.IsNullOrEmpty(sub) ? "" : $"/{sub}";
                Response.Headers["Location"] = $"/api/admin/hangfire-proxy{prefix}?access_token={Uri.EscapeDataString(token)}";
            }
        }

        return new FileContentResult(bytes, mediaType);
    }

    private string ReadToken()
    {
        if (Request.Query.TryGetValue("access_token", out var q) && !string.IsNullOrEmpty(q))
            return q!;

        var auth = Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return auth.Substring(7).Trim();

        if (Request.Cookies.TryGetValue(AuthCookieName, out var c) && !string.IsNullOrEmpty(c))
            return c;

        if (Request.Cookies.TryGetValue("losm_token", out var lt) && !string.IsNullOrEmpty(lt))
            return lt;

        if (Request.Cookies.TryGetValue("scalar_token", out var st) && !string.IsNullOrEmpty(st))
            return st;

        return string.Empty;
    }

    private TokenValidation? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParams = _jwtOptions.TokenValidationParameters;
            handler.ValidateToken(token, validationParams, out var validated);

            if (validated is not JwtSecurityToken jwt)
                return null;

            var role = jwt.Claims
                .FirstOrDefault(c =>
                    c.Type == ClaimTypes.Role ||
                    c.Type == "role" ||
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                ?.Value;

            return new TokenValidation(
                IsAdmin: string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private string BuildInternalUrl(string innerPath, string? query)
    {
        var scheme = _config["Hangfire:InternalScheme"]
            ?? throw new InvalidOperationException(
                "Hangfire:InternalScheme not configured. " +
                "Set it in appsettings.json or via env var Hangfire__InternalScheme.");
        var host = _config["Hangfire:InternalHost"]
            ?? throw new InvalidOperationException(
                "Hangfire:InternalHost not configured. " +
                "Set it in appsettings.json or via env var Hangfire__InternalHost.");

        if (innerPath.StartsWith("hangfire/", StringComparison.OrdinalIgnoreCase))
        {
            innerPath = innerPath.Substring("hangfire/".Length);
        }
        else if (string.Equals(innerPath, "hangfire", StringComparison.OrdinalIgnoreCase))
        {
            innerPath = string.Empty;
        }

        var path = string.IsNullOrEmpty(innerPath) ? "/hangfire/" : $"/hangfire/{innerPath}";
        return $"{scheme}://{host}{path}{query ?? ""}";
    }

    private sealed record TokenValidation(bool IsAdmin);
}