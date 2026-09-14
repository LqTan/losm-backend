namespace AgentCore.Domain.Entities;

public sealed class LastSearchContext
{
    public Guid SessionId { get; private set; }
    public string Query { get; private set; } = null!;
    public double CenterLatitude { get; private set; }
    public double CenterLongitude { get; private set; }
    public double RadiusKm { get; private set; }
    public string ResultPlaceIdsJson { get; private set; } = "[]";
    public string FiltersJson { get; private set; } = "{}";
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private LastSearchContext() { }

    public LastSearchContext(
        Guid sessionId,
        string query,
        double centerLatitude,
        double centerLongitude,
        double radiusKm,
        string resultPlaceIdsJson,
        string filtersJson)
    {
        SessionId = sessionId;
        Query = query;
        CenterLatitude = centerLatitude;
        CenterLongitude = centerLongitude;
        RadiusKm = radiusKm;
        ResultPlaceIdsJson = resultPlaceIdsJson;
        FiltersJson = filtersJson;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Update(
        string query,
        double centerLatitude,
        double centerLongitude,
        double radiusKm,
        string resultPlaceIdsJson,
        string filtersJson)
    {
        Query = query;
        CenterLatitude = centerLatitude;
        CenterLongitude = centerLongitude;
        RadiusKm = radiusKm;
        ResultPlaceIdsJson = resultPlaceIdsJson;
        FiltersJson = filtersJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
