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
using Places.Infrastructure.ExternalServices.Photon.Models;

namespace Places.Infrastructure.Providers;

public sealed class OverpassPlaceProvider : IPlaceProvider, IGeocodingService
{
    public const string NominatimClientName = "Nominatim";
    public const string OverpassClientName = "Overpass";
    public const string PhotonClientName = "Photon";

    private const double MaxPhotonBiasScale = 3.0;

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

        List<NominatimResult>? hits = null;

        if (_options.PhotonsEnabled)
        {
            hits = await FetchPhotonAsync(
                query, includeLocationBias: true,
                latitude, longitude, limit, cancellationToken);
        }

        if (hits is null || hits.Count == 0)
        {
            _logger.LogInformation(
                "Photon search unavailable/empty for query={Query}; falling back to Nominatim",
                query);

            hits = await FetchNominatimAsync(
                query, latitude, longitude, radiusKm, limit, amenity,
                bounded: true, cancellationToken);

            if (hits is null || hits.Count == 0)
            {
                _logger.LogInformation(
                    "Nominatim bounded search returned 0 for query={Query}; retrying with bounded=0 and expanded radius",
                    query);

                hits = await FetchNominatimAsync(
                    query, latitude, longitude, radiusKm * 3, limit, amenity,
                    bounded: false, cancellationToken);
            }
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

    private async Task<List<NominatimResult>?> FetchNominatimAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        string? amenity,
        bool bounded,
        CancellationToken cancellationToken)
    {
        var (lonMin, latMin, lonMax, latMax) = ComputeBoundingBox(latitude, longitude, radiusKm);
        var viewbox = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6},{3:F6}",
            lonMin, latMax, lonMax, latMin);

        var amenityPart = string.IsNullOrWhiteSpace(amenity)
            ? string.Empty
            : $"&amenity={Uri.EscapeDataString(amenity.Trim())}";

        var boundedPart = bounded ? "&bounded=1" : string.Empty;

        var url =
            $"/search?q={Uri.EscapeDataString(query ?? string.Empty)}" +
            $"&format=jsonv2&limit={limit}" +
            $"&viewbox={viewbox}{boundedPart}&addressdetails=0&extratags=1&namedetails=1" +
            amenityPart;

        try
        {
            using var nominatim = _httpClientFactory.CreateClient(NominatimClientName);
            return await nominatim.GetFromJsonAsync<List<NominatimResult>>(
                url,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogError(ex,
                "Nominatim search failed for query={Query} bounded={Bounded} radiusKm={Radius}",
                query, bounded, radiusKm);
            return null;
        }
    }

    public async Task<GeocodedPlace?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        if (_options.PhotonsEnabled)
        {
            var photonHits = await FetchPhotonAsync(
                query, includeLocationBias: false, latitude: 0, longitude: 0,
                limit: 1, cancellationToken);

            var photonFirst = photonHits?.FirstOrDefault();
            if (photonFirst is not null)
            {
                return MapToGeocodedPlace(photonFirst, query);
            }

            _logger.LogInformation(
                "Photon geocode returned 0 for query={Query}; falling back to Nominatim",
                query);
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

            return MapToGeocodedPlace(first, query);
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

    private static GeocodedPlace? MapToGeocodedPlace(NominatimResult first, string fallbackQuery)
    {
        if (!TryParseCoord(first.Lat, out var lat) ||
            !TryParseCoord(first.Lon, out var lon))
        {
            return null;
        }

        return new GeocodedPlace(
            first.OsmType,
            first.OsmId,
            first.Name ?? fallbackQuery,
            first.DisplayName ?? fallbackQuery,
            lat,
            lon,
            first.Category,
            first.Type,
            ParseBoundingBox(first.BoundingBox));
    }

    public async Task<IReadOnlyList<Place>> SearchByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int candidateLimit,
        string? amenity,
        CancellationToken cancellationToken = default)
    {
        if (!BoundingBox.IsValid(boundingBox))
        {
            _logger.LogWarning(
                "Invalid bounding box supplied for query={Query}; skipping search",
                query);
            return [];
        }

        var limit = Math.Max(1, candidateLimit);

        List<NominatimResult>? hits = null;

        if (_options.PhotonsEnabled)
        {
            hits = await FetchPhotonByBoundingBoxAsync(
                query, boundingBox, limit, cancellationToken);
        }

        if (hits is null || hits.Count == 0)
        {
            _logger.LogInformation(
                "Photon bbox search unavailable/empty for query={Query}; falling back to Nominatim",
                query);

            hits = await FetchNominatimByBoundingBoxAsync(
                query, boundingBox, limit, amenity,
                bounded: true, cancellationToken);

            if (hits is null || hits.Count == 0)
            {
                _logger.LogInformation(
                    "Nominatim bounded search returned 0 for query={Query}; retrying with bounded=0",
                    query);

                hits = await FetchNominatimByBoundingBoxAsync(
                    query, boundingBox, limit, amenity,
                    bounded: false, cancellationToken);
            }
        }

        if (hits is null || hits.Count == 0)
        {
            _logger.LogInformation(
                "Overpass search by bounding box returned 0 hits for query={Query}",
                query);
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
            "Overpass search by bounding box returned {Total} hits, {Mapped} valid for query={Query}",
            hits.Count, places.Count, query);

        return places;
    }

    private async Task<List<NominatimResult>?> FetchNominatimByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int limit,
        string? amenity,
        bool bounded,
        CancellationToken cancellationToken)
    {
        var viewbox = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6},{3:F6}",
            boundingBox.MinLongitude,
            boundingBox.MaxLatitude,
            boundingBox.MaxLongitude,
            boundingBox.MinLatitude);

        var amenityPart = string.IsNullOrWhiteSpace(amenity)
            ? string.Empty
            : $"&amenity={Uri.EscapeDataString(amenity.Trim())}";

        var boundedPart = bounded ? "&bounded=1" : string.Empty;

        var url =
            $"/search?q={Uri.EscapeDataString(query ?? string.Empty)}" +
            $"&format=jsonv2&limit={limit}" +
            $"&viewbox={viewbox}{boundedPart}&addressdetails=0&extratags=1&namedetails=1" +
            amenityPart;

        try
        {
            using var nominatim = _httpClientFactory.CreateClient(NominatimClientName);
            return await nominatim.GetFromJsonAsync<List<NominatimResult>>(
                url,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogError(ex,
                "Nominatim search by bounding box failed for query={Query} bounded={Bounded}",
                query, bounded);
            return null;
        }
    }

    private async Task<List<NominatimResult>?> FetchPhotonAsync(
        string query,
        bool includeLocationBias,
        double latitude,
        double longitude,
        int limit,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.Append("?q=").Append(Uri.EscapeDataString(query?.Trim() ?? string.Empty));
        sb.Append("&limit=").Append(limit);
        if (includeLocationBias)
        {
            sb.Append("&lat=").AppendFormat(
                CultureInfo.InvariantCulture, "{0:F6}", latitude);
            sb.Append("&lon=").AppendFormat(
                CultureInfo.InvariantCulture, "{0:F6}", longitude);
            sb.Append("&location_bias_scale=").AppendFormat(
                CultureInfo.InvariantCulture, "{0:F2}", MaxPhotonBiasScale);
        }

        var url = sb.ToString();

        try
        {
            using var photon = _httpClientFactory.CreateClient(PhotonClientName);
            var response = await photon.GetFromJsonAsync<PhotonFeatureCollection>(
                url, cancellationToken);
            var features = response?.Features;
            if (features is null || features.Count == 0)
            {
                return new List<NominatimResult>(0);
            }

            return features.Select(MapPhotonFeature).ToList();
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Photon fetch failed for query={Query} ({Status})",
                query, ex.Message);
            return null;
        }
    }

    private async Task<List<NominatimResult>?> FetchPhotonByBoundingBoxAsync(
        string query,
        BoundingBox boundingBox,
        int limit,
        CancellationToken cancellationToken)
    {
        var bboxParam = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F6},{1:F6},{2:F6},{3:F6}",
            boundingBox.MinLongitude,
            boundingBox.MinLatitude,
            boundingBox.MaxLongitude,
            boundingBox.MaxLatitude);

        var url =
            $"?q={Uri.EscapeDataString(query?.Trim() ?? string.Empty)}" +
            $"&limit={limit}&bbox={bboxParam}";

        try
        {
            using var photon = _httpClientFactory.CreateClient(PhotonClientName);
            var response = await photon.GetFromJsonAsync<PhotonFeatureCollection>(
                url, cancellationToken);
            var features = response?.Features;
            if (features is null || features.Count == 0)
            {
                return new List<NominatimResult>(0);
            }

            return features.Select(MapPhotonFeature).ToList();
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Photon bbox fetch failed for query={Query} ({Status})",
                query, ex.Message);
            return null;
        }
    }

    private static NominatimResult MapPhotonFeature(PhotonFeature feature)
    {
        double lat = 0, lon = 0;
        var coords = feature.Geometry?.Coordinates;
        if (coords is { Count: >= 2 })
        {
            lon = coords[0];
            lat = coords[1];
        }

        var props = feature.Properties;

        var name = !string.IsNullOrWhiteSpace(props?.Name)
            ? props.Name
            : $"{props?.OsmKey} {props?.OsmValue}".Trim();

        var displayName = BuildPhotonDisplayName(props);

        var category = props?.OsmKey;
        var type = props?.OsmValue;

        var boundingBoxRaw = props?.Extent is { Count: >= 4 } ext
            ? new List<string>
            {
                ext[1].ToString(CultureInfo.InvariantCulture),
                ext[3].ToString(CultureInfo.InvariantCulture),
                ext[0].ToString(CultureInfo.InvariantCulture),
                ext[2].ToString(CultureInfo.InvariantCulture)
            }
            : null;

        return new NominatimResult
        {
            OsmType = NormalizeOsmType(props?.OsmType),
            OsmId = props?.OsmId ?? 0,
            Name = name,
            DisplayName = displayName,
            Lat = lat.ToString(CultureInfo.InvariantCulture),
            Lon = lon.ToString(CultureInfo.InvariantCulture),
            Category = category,
            Type = type,
            BoundingBox = boundingBoxRaw
        };
    }

    private static string NormalizeOsmType(string? raw) => raw switch
    {
        "N" or "n" => "node",
        "W" or "w" => "way",
        "R" or "r" => "relation",
        _ => "node"
    };

    private static string BuildPhotonDisplayName(PhotonProperties? props)
    {
        if (props is null) return string.Empty;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(props.Housenumber))
            parts.Add($"{props.Housenumber}");
        if (!string.IsNullOrWhiteSpace(props.Street))
            parts.Add(props.Street);
        if (!string.IsNullOrWhiteSpace(props.Suburb))
            parts.Add(props.Suburb);
        if (!string.IsNullOrWhiteSpace(props.District))
            parts.Add(props.District);
        if (!string.IsNullOrWhiteSpace(props.City))
            parts.Add(props.City);
        if (!string.IsNullOrWhiteSpace(props.State))
            parts.Add(props.State);
        if (!string.IsNullOrWhiteSpace(props.Country))
            parts.Add(props.Country);
        return string.Join(", ", parts);
    }

    private static BoundingBox? ParseBoundingBox(List<string>? raw)
    {
        if (raw is null || raw.Count < 4) return null;
        if (!double.TryParse(raw[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLat) ||
            !double.TryParse(raw[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLat) ||
            !double.TryParse(raw[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var minLon) ||
            !double.TryParse(raw[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var maxLon))
        {
            return null;
        }

        var box = new BoundingBox(minLat, maxLat, minLon, maxLon);
        return BoundingBox.IsValid(box) ? box : null;
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


