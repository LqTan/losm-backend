using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Places.Application.Abstractions;
using Places.Domain.Entities;
using Places.Infrastructure.ExternalServices.Nominatim.Models;
using Places.Infrastructure.ExternalServices.Overpass.Models;

namespace Places.Infrastructure.Providers;

public sealed class OverpassPlaceProvider : IPlaceProvider, IGeocodingService
{
    public const string NominatimClientName = "Nominatim";
    public const string OverpassClientName = "Overpass";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OverpassProviderOptions _options;
    private readonly ILogger<OverpassPlaceProvider> _logger;

    public OverpassPlaceProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<OverpassProviderOptions> options,
        ILogger<OverpassPlaceProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        CancellationToken cancellationToken = default)
    {
        return SearchAsync(
            query,
            latitude,
            longitude,
            radiusKm,
            candidateLimit,
            amenity: null,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Max(1, candidateLimit);
        var (lonMin, latMin, lonMax, latMax) = ComputeBoundingBox(latitude, longitude, radiusKm);
        var viewbox = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6},{3:F6}",
            lonMin, latMax, lonMax, latMin);

        var amenityPart = string.IsNullOrWhiteSpace(amenity)
            ? string.Empty
            : $"&amenity={Uri.EscapeDataString(amenity.Trim())}";

        var nominatimUrl =
            $"/search?q={Uri.EscapeDataString(query ?? string.Empty)}" +
            $"&format=jsonv2&limit={limit}" +
            $"&viewbox={viewbox}&bounded=1&addressdetails=0&extratags=1&namedetails=1" +
            amenityPart;

        List<NominatimResult>? hits;
        try
        {
            using var nominatim = _httpClientFactory.CreateClient(NominatimClientName);
            hits = await nominatim.GetFromJsonAsync<List<NominatimResult>>(
                nominatimUrl,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogError(ex, "Nominatim search failed for query={Query}", query);
            return [];
        }

        if (hits is null || hits.Count == 0)
        {
            _logger.LogInformation(
                "Overpass search returned 0 hits for query={Query} amenity={Amenity} at ({Lat},{Lon})",
                query, amenity, latitude, longitude);
            return [];
        }

        var openingHoursMap = await FetchOpeningHoursAsync(hits, cancellationToken);

        var places = new List<Place>(hits.Count);
        foreach (var h in hits)
        {
            if (string.IsNullOrWhiteSpace(h.OsmType) ||
                string.IsNullOrWhiteSpace(h.Name) ||
                !TryParseCoord(h.Lat, out var lat) ||
                !TryParseCoord(h.Lon, out var lon))
            {
                continue;
            }

            openingHoursMap.TryGetValue((h.OsmType, h.OsmId), out var openingHours);

            places.Add(new Place(
                externalId: $"{h.OsmType}:{h.OsmId}",
                name: h.Name!,
                latitude: lat,
                longitude: lon,
                source: "Overpass",
                address: h.DisplayName,
                category: BuildCategory(h),
                openingHours: openingHours));
        }

        _logger.LogInformation(
            "Overpass search returned {Total} hits, {Mapped} valid for query={Query} amenity={Amenity} at ({Lat},{Lon})",
            hits.Count, places.Count, query, amenity, latitude, longitude);

        return places;
    }

    public async Task<GeocodedPlace?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var url =
            $"/search?q={Uri.EscapeDataString(query.Trim())}" +
            $"&format=jsonv2&limit=1" +
            $"&countrycodes=vn&addressdetails=0&namedetails=1";

        try
        {
            using var nominatim = _httpClientFactory.CreateClient(NominatimClientName);
            var hits = await nominatim.GetFromJsonAsync<List<NominatimResult>>(
                url,
                cancellationToken);

            var first = hits?.FirstOrDefault();
            if (first is null) return null;

            if (!TryParseCoord(first.Lat, out var lat) ||
                !TryParseCoord(first.Lon, out var lon))
            {
                return null;
            }

            return new GeocodedPlace(
                first.OsmType,
                first.OsmId,
                first.Name ?? query,
                first.DisplayName ?? query,
                lat,
                lon,
                first.Category,
                first.Type);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogError(ex, "Nominatim geocode failed for query={Query}", query);
            return null;
        }
    }

    private async Task<Dictionary<(string Type, long Id), string?>> FetchOpeningHoursAsync(
        List<NominatimResult> hits,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, long), string?>();
        if (hits.Count == 0) return result;

        var nodeIds = hits
            .Where(h => string.Equals(h.OsmType, "node", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.OsmId)
            .ToList();
        var wayIds = hits
            .Where(h => string.Equals(h.OsmType, "way", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.OsmId)
            .ToList();
        var relationIds = hits
            .Where(h => string.Equals(h.OsmType, "relation", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.OsmId)
            .ToList();

        if (nodeIds.Count == 0 && wayIds.Count == 0 && relationIds.Count == 0)
        {
            return result;
        }

        var query = new StringBuilder("[out:json];");
        if (nodeIds.Count > 0)
            query.Append("node(id:").AppendJoin(',', nodeIds).Append(");");
        if (wayIds.Count > 0)
            query.Append("way(id:").AppendJoin(',', wayIds).Append(");");
        if (relationIds.Count > 0)
            query.Append("relation(id:").AppendJoin(',', relationIds).Append(");");
        query.Append("out tags;");

        try
        {
            using var overpass = _httpClientFactory.CreateClient(OverpassClientName);
            using var content = new FormUrlEncodedContent(
                new[] { new KeyValuePair<string, string>("data", query.ToString()) });
            using var resp = await overpass.PostAsync("interpreter", content, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Overpass tag enrichment returned {Status} for {Count} hits",
                    resp.StatusCode, hits.Count);
                return result;
            }

            var parsed = await resp.Content.ReadFromJsonAsync<OverpassResponse>(
                cancellationToken: cancellationToken);
            if (parsed?.Elements is null) return result;

            foreach (var el in parsed.Elements)
            {
                string? oh = null;
                if (el.Tags is not null &&
                    el.Tags.TryGetValue("opening_hours", out var v))
                {
                    oh = string.IsNullOrWhiteSpace(v) ? null : v;
                }
                result[(el.Type, el.Id)] = oh;
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(ex,
                "Overpass tag enrichment failed; continuing without opening_hours");
        }

        return result;
    }

    private static string? BuildCategory(NominatimResult h)
    {
        if (!string.IsNullOrWhiteSpace(h.Category) && !string.IsNullOrWhiteSpace(h.Type))
            return $"{h.Category}/{h.Type}";
        if (!string.IsNullOrWhiteSpace(h.Category))
            return h.Category;
        if (!string.IsNullOrWhiteSpace(h.Type))
            return h.Type;
        return null;
    }

    private static bool TryParseCoord(string raw, out double value)
    {
        return double.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static (double LonMin, double LatMin, double LonMax, double LatMax) ComputeBoundingBox(
        double lat,
        double lon,
        double radiusKm)
    {
        var dLat = radiusKm / 111.0;
        var cosLat = Math.Max(0.000001, Math.Cos(lat * Math.PI / 180.0));
        var dLon = radiusKm / (111.0 * cosLat);
        return (lon - dLon, lat - dLat, lon + dLon, lat + dLat);
    }
}


