namespace Places.Infrastructure.Providers;

public sealed class OverpassProviderOptions
{
    public string NominatimBaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";
    public string OverpassBaseUrl { get; set; } = "https://overpass-api.de/api/";
    public string UserAgent { get; set; } = "losm-backend/1.0";
    public int TimeoutSeconds { get; set; } = 10;
}
