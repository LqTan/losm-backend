using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Search.Application.Abstractions;

namespace Search.Infrastructure.Services;

public sealed class OsrmTableService : IOsrmTableService
{
    private readonly HttpClient _httpClient;
    private readonly OsrmOptions _options;
    private readonly ILogger<OsrmTableService> _logger;

    public OsrmTableService(
        HttpClient httpClient,
        IOptions<OsrmOptions> options,
        ILogger<OsrmTableService> logger
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

    public async Task<OsrmTableMatrix?> GetTableAsync(
        IReadOnlyList<GeoCoordinate> sources,
        IReadOnlyList<GeoCoordinate> destinations,
        CancellationToken cancellationToken = default
    )
    {
        if (!_options.Enabled || sources.Count == 0 || destinations.Count == 0)
        {
            return null;
        }

        try
        {
            // OSRM format: lon,lat separated by semicolons
            var allCoords = new List<string>(sources.Count + destinations.Count);

            foreach (var s in sources)
            {
                allCoords.Add(string.Create(CultureInfo.InvariantCulture, $"{s.Longitude:F6},{s.Latitude:F6}"));
            }

            foreach (var d in destinations)
            {
                allCoords.Add(string.Create(CultureInfo.InvariantCulture, $"{d.Longitude:F6},{d.Latitude:F6}"));
            }

            var coordsParam = string.Join(";", allCoords);
            var sourceIndices = string.Join(";", Enumerable.Range(0, sources.Count));
            var destIndices = string.Join(";", Enumerable.Range(sources.Count, destinations.Count));

            var requestUri = $"table/v1/driving/{coordsParam}?sources={sourceIndices}&destinations={destIndices}&annotations=duration,distance";

            var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OSRM Table API returned status {StatusCode}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OsrmApiResponse>(cancellationToken: cancellationToken);
            if (result is null || !string.Equals(result.Code, "Ok", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("OSRM Table API returned code {Code}", result?.Code);
                return null;
            }

            if (result.Distances is null || result.Durations is null)
            {
                return null;
            }

            // Distances: meters -> km (Math.Round(m / 1000.0, 2))
            var distancesKm = new List<IReadOnlyList<double>>(result.Distances.Count);
            foreach (var row in result.Distances)
            {
                var convertedRow = row
                    .Select(d => d.HasValue ? Math.Round(d.Value / 1000.0, 2) : 999.0)
                    .ToList();
                distancesKm.Add(convertedRow);
            }

            // Durations: seconds -> minutes (Math.Round(s / 60.0, 1))
            var durationsMin = new List<IReadOnlyList<double>>(result.Durations.Count);
            foreach (var row in result.Durations)
            {
                var convertedRow = row
                    .Select(t => t.HasValue ? Math.Round(t.Value / 60.0, 1) : 999.0)
                    .ToList();
                durationsMin.Add(convertedRow);
            }

            return new OsrmTableMatrix(distancesKm, durationsMin);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call OSRM Table API. Falling back to geometric distance.");
            return null;
        }
    }

    private sealed class OsrmApiResponse
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("distances")]
        public List<List<double?>>? Distances { get; set; }

        [JsonPropertyName("durations")]
        public List<List<double?>>? Durations { get; set; }
    }
}
