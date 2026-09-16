namespace AgentCore.Domain.Entities;

public sealed class UserGoogleToken
{
    public Guid UserId { get; private set; }
    public string GoogleSub { get; private set; } = null!;
    public string GoogleEmail { get; set; } = string.Empty;
    public string AccessToken { get; private set; } = null!;
    public string RefreshToken { get; private set; } = null!;
    public DateTime AccessTokenExpiresAt { get; private set; }
    public string Scopes { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private UserGoogleToken() { }

    public UserGoogleToken(
        Guid userId,
        string googleSub,
        string googleEmail,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        string scopes)
    {
        UserId = userId;
        GoogleSub = googleSub;
        GoogleEmail = googleEmail;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        Scopes = scopes;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void UpdateTokens(
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt,
        string googleEmail)
    {
        AccessToken = accessToken;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            RefreshToken = refreshToken;
        }
        AccessTokenExpiresAt = accessTokenExpiresAt;
        if (!string.IsNullOrWhiteSpace(googleEmail))
        {
            GoogleEmail = googleEmail;
        }
        UpdatedAt = DateTime.UtcNow;
    }
}