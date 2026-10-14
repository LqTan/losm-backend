using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class OsrmRouteService : IOsrmRouteService
{
    private readonly HttpClient _httpClient;
    private readonly OsrmOptions _options;
    private readonly ILogger<OsrmRouteService> _logger;

    public OsrmRouteService(
        HttpClient httpClient,
        IOptions<OsrmOptions> options,
        ILogger<OsrmRouteService> logger
    )
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public async Task<OsrmRouteResult?> GetRouteAsync(
        GeoCoordinate origin,
        GeoCoordinate destination,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.Enabled)
        {
            return null;
        }

        try
        {
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"route/v1/driving/{origin.Longitude:F6},{origin.Latitude:F6};{destination.Longitude:F6},{destination.Latitude:F6}?overview=full&geometries=geojson"
            );

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM Route API returned status {StatusCode}", response.StatusCode);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken
            );

            var root = doc.RootElement;
            if (!root.TryGetProperty("code", out var codeProp) || codeProp.GetString() != "Ok")
            {
                _logger.LogWarning("OSRM Route API returned non-Ok code");
                return null;
            }

            if (!root.TryGetProperty("routes", out var routesProp) || routesProp.GetArrayLength() == 0)
            {
                return null;
            }

            var bestRoute = routesProp[0];
            var distanceMeters = bestRoute.TryGetProperty("distance", out var distProp) ? distProp.GetDouble() : 0.0;
            var durationSeconds = bestRoute.TryGetProperty("duration", out var durProp) ? durProp.GetDouble() : 0.0;

            string? summary = null;
            if (bestRoute.TryGetProperty("legs", out var legsProp) && legsProp.GetArrayLength() > 0)
            {
                var firstLeg = legsProp[0];
                if (firstLeg.TryGetProperty("summary", out var sumProp))
                {
                    summary = sumProp.GetString();
                }
            }

            var coordinates = new List<GeoCoordinate>();
            if (bestRoute.TryGetProperty("geometry", out var geomProp) &&
                geomProp.TryGetProperty("coordinates", out var coordsProp) &&
                coordsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var coordPair in coordsProp.EnumerateArray())
                {
                    if (coordPair.ValueKind == JsonValueKind.Array && coordPair.GetArrayLength() >= 2)
                    {
                        var lon = coordPair[0].GetDouble();
                        var lat = coordPair[1].GetDouble();
                        coordinates.Add(new GeoCoordinate(lat, lon));
                    }
                }
            }

            return new OsrmRouteResult(
                DistanceKm: distanceMeters / 1000.0,
                DurationMinutes: durationSeconds / 60.0,
                Coordinates: coordinates,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call OSRM Route API.");
            return null;
        }
    }
}
