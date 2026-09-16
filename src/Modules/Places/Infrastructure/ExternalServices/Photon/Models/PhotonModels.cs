using System.Text.Json.Serialization;

namespace Places.Infrastructure.ExternalServices.Photon.Models;

public sealed class PhotonFeatureCollection
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonPropertyName("features")]
    public List<PhotonFeature> Features { get; set; } = new();
}

public sealed class PhotonFeature
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Feature";

    [JsonPropertyName("geometry")]
    public PhotonGeometry? Geometry { get; set; }

    [JsonPropertyName("properties")]
    public PhotonProperties? Properties { get; set; }
}

public sealed class PhotonGeometry
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Point";

    [JsonPropertyName("coordinates")]
    public List<double>? Coordinates { get; set; }
}

public sealed class PhotonProperties
{
    [JsonPropertyName("osm_id")]
    public long OsmId { get; set; }

    [JsonPropertyName("osm_type")]
    public string? OsmType { get; set; }

    [JsonPropertyName("osm_key")]
    public string? OsmKey { get; set; }

    [JsonPropertyName("osm_value")]
    public string? OsmValue { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("housenumber")]
    public string? Housenumber { get; set; }

    [JsonPropertyName("suburb")]
    public string? Suburb { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; set; }

    [JsonPropertyName("countrycode")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("extent")]
    public List<double>? Extent { get; set; }
}