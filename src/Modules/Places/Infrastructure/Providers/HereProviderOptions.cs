namespace Places.Infrastructure.Providers;

public sealed class HereProviderOptions
{
    public string BaseUrl { get; set; } = "https://discover.search.hereapi.com/";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 5;
}
