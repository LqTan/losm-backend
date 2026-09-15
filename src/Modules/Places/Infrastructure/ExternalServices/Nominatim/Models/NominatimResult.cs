namespace Places.Infrastructure.ExternalServices.Nominatim.Models;

internal sealed class NominatimResult
{
    public long PlaceId { get; set; }
    public string OsmType { get; set; } = string.Empty;
    public long OsmId { get; set; }
    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
}
