namespace AgentCore.Infrastructure.Google;

public sealed class GoogleOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
    public string ApplicationName { get; set; } = "LocationSearch.Api";
}