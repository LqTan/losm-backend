using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Overture.Infrastructure;

public interface IOvertureItemStreamer
{
    IAsyncEnumerable<OvertureItemRecord> StreamAsync(
        string jsonlPath,
        CancellationToken cancellationToken = default);
}

public sealed record OvertureItemRecord(
    string Id,
    string Name,
    double Lat,
    double Lng,
    string? Address,
    string? Category);

public sealed class OvertureItemStreamer : IOvertureItemStreamer
{
    private readonly ILogger<OvertureItemStreamer> _logger;

    public OvertureItemStreamer(ILogger<OvertureItemStreamer> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<OvertureItemRecord> StreamAsync(
        string jsonlPath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jsonlPath))
        {
            _logger.LogWarning(
                "Overture file not found at {Path}; returning empty stream.",
                jsonlPath);
            yield break;
        }

        using var stream = File.OpenRead(jsonlPath);
        using var reader = new StreamReader(stream);

        var count = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Skip GeoJSON FeatureCollection wrapper header if on its own line
            if (trimmed.StartsWith("{\"type\": \"FeatureCollection\"", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("{\"type\":\"FeatureCollection\"", StringComparison.OrdinalIgnoreCase))
            {
                var bracketIdx = trimmed.IndexOf('[');
                if (bracketIdx >= 0)
                {
                    trimmed = trimmed.Substring(bracketIdx + 1).Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;
                }
                else
                {
                    continue;
                }
            }

            // Strip trailing GeoJSON array closing tokens: ]} or ]
            if (trimmed.EndsWith("]}"))
            {
                trimmed = trimmed[..^2].TrimEnd();
            }
            else if (trimmed.EndsWith(']'))
            {
                trimmed = trimmed[..^1].TrimEnd();
            }

            // Strip trailing comma between array items
            if (trimmed.EndsWith(','))
            {
                trimmed = trimmed[..^1].TrimEnd();
            }

            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            OvertureItemRecord? record = null;
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                record = ParseRecord(doc.RootElement, count);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Skipping malformed Overture line {Count}",
                    count + 1);
                count++;
                continue;
            }

            if (record is null)
            {
                count++;
                continue;
            }

            count++;
            yield return record;
        }

        _logger.LogInformation(
            "Overture file parsed: {Count} items from {Path}",
            count, jsonlPath);
    }

    private static OvertureItemRecord? ParseRecord(JsonElement root, int count)
    {
        var hasProperties = root.TryGetProperty("properties", out var props) &&
                            props.ValueKind == JsonValueKind.Object;

        var (lat, lng) = ExtractCoordinates(root, hasProperties ? props : default);
        if (lat is null || lng is null) return null;

        var id = ExtractId(root, hasProperties ? props : default, lat.Value, lng.Value, count);
        var name = ExtractName(root, hasProperties ? props : default) ?? id;
        var category = ExtractCategory(root, hasProperties ? props : default);
        var address = ExtractAddress(root, hasProperties ? props : default);

        return new OvertureItemRecord(
            Id: id,
            Name: name,
            Lat: lat.Value,
            Lng: lng.Value,
            Address: address,
            Category: category);
    }

    private static (double? lat, double? lng) ExtractCoordinates(JsonElement root, JsonElement props)
    {
        // 1. GeoJSON geometry.coordinates: [lng, lat]
        if (root.TryGetProperty("geometry", out var geom) &&
            geom.ValueKind == JsonValueKind.Object &&
            geom.TryGetProperty("coordinates", out var coords) &&
            coords.ValueKind == JsonValueKind.Array &&
            coords.GetArrayLength() >= 2)
        {
            if (TryGetDouble(coords[0], out var lng) && TryGetDouble(coords[1], out var lat))
            {
                if (IsValidCoord(lat, lng)) return (lat, lng);
            }
        }

        // 2. Root coordinates array: [lng, lat]
        if (root.TryGetProperty("coordinates", out var rootCoords) &&
            rootCoords.ValueKind == JsonValueKind.Array &&
            rootCoords.GetArrayLength() >= 2)
        {
            if (TryGetDouble(rootCoords[0], out var lng) && TryGetDouble(rootCoords[1], out var lat))
            {
                if (IsValidCoord(lat, lng)) return (lat, lng);
            }
        }

        // 3. Root latitude/longitude
        if (TryExtractLatLon(root, out var rLat, out var rLng))
        {
            return (rLat, rLng);
        }

        // 4. Properties latitude/longitude
        if (props.ValueKind == JsonValueKind.Object && TryExtractLatLon(props, out var pLat, out var pLng))
        {
            return (pLat, pLng);
        }

        return (null, null);
    }

    private static bool TryExtractLatLon(JsonElement elem, out double? lat, out double? lng)
    {
        lat = null;
        lng = null;

        double latVal = 0;
        double lngVal = 0;

        var hasLat = (elem.TryGetProperty("latitude", out var latElem) || elem.TryGetProperty("lat", out latElem)) &&
                     TryGetDouble(latElem, out latVal);
        var hasLng = (elem.TryGetProperty("longitude", out var lngElem) || elem.TryGetProperty("lng", out lngElem) ||
                      elem.TryGetProperty("lon", out lngElem)) &&
                     TryGetDouble(lngElem, out lngVal);

        if (hasLat && hasLng && IsValidCoord(latVal, lngVal))
        {
            lat = latVal;
            lng = lngVal;
            return true;
        }

        return false;
    }

    private static bool IsValidCoord(double lat, double lng)
        => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;

    private static string ExtractId(JsonElement root, JsonElement props, double lat, double lng, int count)
    {
        if (root.TryGetProperty("id", out var idElem) && idElem.ValueKind is JsonValueKind.String)
        {
            var str = idElem.GetString();
            if (!string.IsNullOrWhiteSpace(str)) return str;
        }

        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("id", out var pId) &&
            pId.ValueKind is JsonValueKind.String)
        {
            var str = pId.GetString();
            if (!string.IsNullOrWhiteSpace(str)) return str;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0},{1}-{2}", lat, lng, count);
    }

    private static string? ExtractName(JsonElement root, JsonElement props)
    {
        // 1. properties.names.primary
        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("names", out var names) &&
            names.ValueKind == JsonValueKind.Object)
        {
            if (names.TryGetProperty("primary", out var prim) && prim.ValueKind == JsonValueKind.String)
            {
                var s = prim.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }

            if (names.TryGetProperty("common", out var common))
            {
                if (common.ValueKind == JsonValueKind.String)
                {
                    var s = common.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
                else if (common.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in common.EnumerateObject())
                    {
                        var s = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }
            }
        }

        // 2. properties.name
        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("name", out var pName) &&
            pName.ValueKind == JsonValueKind.String)
        {
            var s = pName.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        // 3. root.name
        if (root.TryGetProperty("name", out var rName) && rName.ValueKind == JsonValueKind.String)
        {
            var s = rName.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        return null;
    }

    private static string? ExtractCategory(JsonElement root, JsonElement props)
    {
        // 1. properties.categories.main
        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("categories", out var cats) &&
            cats.ValueKind == JsonValueKind.Object)
        {
            if (cats.TryGetProperty("main", out var main) && main.ValueKind == JsonValueKind.String)
            {
                var s = main.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }

            if (cats.TryGetProperty("alternate", out var alt) &&
                alt.ValueKind == JsonValueKind.Array &&
                alt.GetArrayLength() > 0)
            {
                var s = alt[0].GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }

        // 2. properties.category
        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("category", out var pCat) &&
            pCat.ValueKind == JsonValueKind.String)
        {
            var s = pCat.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        // 3. root.category
        if (root.TryGetProperty("category", out var rCat) && rCat.ValueKind == JsonValueKind.String)
        {
            var s = rCat.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        return null;
    }

    private static string? ExtractAddress(JsonElement root, JsonElement props)
    {
        // 1. properties.addresses: array of address objects
        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("addresses", out var addrs) &&
            addrs.ValueKind == JsonValueKind.Array &&
            addrs.GetArrayLength() > 0)
        {
            var first = addrs[0];
            if (first.ValueKind == JsonValueKind.Object)
            {
                if (first.TryGetProperty("freeform", out var freeform) &&
                    freeform.ValueKind == JsonValueKind.String)
                {
                    var s = freeform.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }

                var parts = new List<string>();
                if (first.TryGetProperty("locality", out var loc) && loc.ValueKind == JsonValueKind.String)
                    parts.Add(loc.GetString()!);
                if (first.TryGetProperty("region", out var reg) && reg.ValueKind == JsonValueKind.String)
                    parts.Add(reg.GetString()!);
                if (first.TryGetProperty("country", out var country) && country.ValueKind == JsonValueKind.String)
                    parts.Add(country.GetString()!);

                if (parts.Count > 0) return string.Join(", ", parts);
            }
            else if (first.ValueKind == JsonValueKind.String)
            {
                return first.GetString();
            }
        }

        // 2. properties.address
        if (props.ValueKind == JsonValueKind.Object &&
            props.TryGetProperty("address", out var pAddr) &&
            pAddr.ValueKind == JsonValueKind.String)
        {
            var s = pAddr.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        // 3. root.address
        if (root.TryGetProperty("address", out var rAddr) && rAddr.ValueKind == JsonValueKind.String)
        {
            var s = rAddr.GetString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }

        return null;
    }

    private static bool TryGetDouble(JsonElement elem, out double val)
    {
        if (elem.ValueKind == JsonValueKind.Number && elem.TryGetDouble(out val))
        {
            return true;
        }
        if (elem.ValueKind == JsonValueKind.String &&
            double.TryParse(elem.GetString(), CultureInfo.InvariantCulture, out val))
        {
            return true;
        }
        val = 0;
        return false;
    }
}