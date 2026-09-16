using System.Text.Json.Serialization;

namespace Places.Infrastructure.ExternalServices.Nominatim.Models;

internal sealed class NominatimResult
{
    [JsonPropertyName("place_id")]
    public long PlaceId { get; set; }

    [JsonPropertyName("osm_type")]
    public string OsmType { get; set; } = string.Empty;

    [JsonPropertyName("osm_id")]
    public long OsmId { get; set; }

    public string Lat { get; set; } = string.Empty;
    public string Lon { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("boundingbox")]
    public List<string>? BoundingBox { get; set; }
}
