using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using GoogleAuthFlows = global::Google.Apis.Auth.OAuth2.Flows;
using GoogleAuthFlowsRequests = global::Google.Apis.Auth.OAuth2.Requests;
using GoogleAuth = global::Google.Apis.Auth.OAuth2;
using GoogleTokenResponse = global::Google.Apis.Auth.OAuth2.Responses.TokenResponse;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentCore.Infrastructure.Google;

public sealed record GoogleOAuthExchangeResult(
    bool Success,
    Guid UserId,
    string GoogleSub,
    string GoogleEmail,
    DateTime AccessTokenExpiresAt,
    string? Error
);

public interface IGoogleOAuthService
{
    string BuildAuthorizationUrl(Guid userId);

    Task<GoogleOAuthExchangeResult> ExchangeCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);
}

public sealed class GoogleOAuthService : IGoogleOAuthService
{
    private readonly GoogleOptions _options;
    private readonly IUserGoogleTokenRepository _repository;
    private readonly ILogger<GoogleOAuthService> _logger;

    public GoogleOAuthService(
        IOptions<GoogleOptions> options,
        IUserGoogleTokenRepository repository,
        ILogger<GoogleOAuthService> logger)
    {
        _options = options.Value;
        _repository = repository;
        _logger = logger;
    }

    public string BuildAuthorizationUrl(Guid userId)
    {
        var redirectUri = string.IsNullOrWhiteSpace(_options.RedirectUri)
            ? "http://localhost:5232/api/users/me/google-calendar/callback"
            : _options.RedirectUri;

        var flow = new GoogleAuthFlows.GoogleAuthorizationCodeFlow(
            new GoogleAuthFlows.GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new GoogleAuth.ClientSecrets
                {
                    ClientId = _options.ClientId,
                    ClientSecret = _options.ClientSecret
                },
                Scopes = _options.Scopes
            });

        var rawRequest = flow.CreateAuthorizationCodeRequest(redirectUri);
        var authorizationUrl = (GoogleAuthFlowsRequests.GoogleAuthorizationCodeRequestUrl)rawRequest;
        authorizationUrl.State = userId.ToString();
        authorizationUrl.AccessType = "offline";
        authorizationUrl.Prompt = "consent";

        return authorizationUrl.Build().AbsoluteUri;
    }

    public async Task<GoogleOAuthExchangeResult> ExchangeCodeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var redirectUri = string.IsNullOrWhiteSpace(_options.RedirectUri)
            ? "http://localhost:5232/api/users/me/google-calendar/callback"
            : _options.RedirectUri;

        var flow = new GoogleAuthFlows.GoogleAuthorizationCodeFlow(
            new GoogleAuthFlows.GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new GoogleAuth.ClientSecrets
                {
                    ClientId = _options.ClientId,
                    ClientSecret = _options.ClientSecret
                },
                Scopes = _options.Scopes
            });

        try
        {
            var token = await flow.ExchangeCodeForTokenAsync(
                userId.ToString(),
                code,
                redirectUri,
                CancellationToken.None);

            var accessExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds ?? 3600);
            var scopeString = token.Scope ?? string.Join(" ", _options.Scopes);

            var existing = await _repository.GetByUserIdAsync(userId, cancellationToken);

            if (existing is null)
            {
                var sub = await ResolveGoogleSubAsync(token.AccessToken);
                var entity = new UserGoogleToken(
                    userId,
                    sub,
                    await ResolveGoogleEmailAsync(token.AccessToken),
                    token.AccessToken,
                    token.RefreshToken ?? string.Empty,
                    accessExpiresAt,
                    scopeString);
                await _repository.AddAsync(entity, cancellationToken);
            }
            else
            {
                existing.UpdateTokens(
                    token.AccessToken,
                    token.RefreshToken ?? existing.RefreshToken,
                    accessExpiresAt,
                    existing.GoogleEmail);
                await _repository.UpdateAsync(existing, cancellationToken);
            }

            var finalToken = await _repository.GetByUserIdAsync(userId, cancellationToken);

            return new GoogleOAuthExchangeResult(
                true,
                userId,
                finalToken?.GoogleSub ?? string.Empty,
                finalToken?.GoogleEmail ?? string.Empty,
                accessExpiresAt,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Google OAuth exchange failed for user {UserId}",
                userId);
            return new GoogleOAuthExchangeResult(
                false,
                userId,
                string.Empty,
                string.Empty,
                DateTime.MinValue,
                ex.Message);
        }
    }

    private static async Task<string> ResolveGoogleSubAsync(string accessToken)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var json = await http.GetStringAsync(
                "https://www.googleapis.com/oauth2/v3/userinfo");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sub", out var sub)
                ? sub.GetString() ?? Guid.NewGuid().ToString()
                : Guid.NewGuid().ToString();
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    private static async Task<string> ResolveGoogleEmailAsync(string accessToken)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var json = await http.GetStringAsync(
                "https://www.googleapis.com/oauth2/v3/userinfo");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("email", out var email)
                ? email.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}