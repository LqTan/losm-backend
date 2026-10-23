namespace AgentCore.Application.Abstractions;

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
