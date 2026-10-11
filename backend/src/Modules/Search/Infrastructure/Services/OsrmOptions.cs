namespace Search.Infrastructure.Services;

public sealed class OsrmOptions
{
    public const string SectionName = "Osrm";

    public string BaseUrl { get; set; } = "http://router.project-osrm.org/";
    public int TimeoutSeconds { get; set; } = 6;
    public bool Enabled { get; set; } = true;
}
