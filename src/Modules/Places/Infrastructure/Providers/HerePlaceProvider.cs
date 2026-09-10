using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;
using Places.Domain.Entities;
using Places.Infrastructure.ExternalServices.Here.Models;

namespace Places.Infrastructure.Providers;

public sealed class HerePlaceProvider : IPlaceProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<HerePlaceProvider> _logger;

    public HerePlaceProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<HerePlaceProvider> logger
    )
    {
        _httpClient = httpClient;
        _apiKey = configuration["Here:ApiKey"]
            ?? throw new InvalidOperationException("HERE API key is missing.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<Place>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        CancellationToken cancellationToken = default
    )
    {
        var radiusMeters = Math.Max(1, (int)(radiusKm * 1000));
        var url =
            $"v1/discover" +
            $"?in=circle:{latitude},{longitude};r={radiusMeters}" +
            $"&q={Uri.EscapeDataString(query ?? string.Empty)}" +
            $"&limit=20" +
            $"&apiKey={_apiKey}";

        try
        {
            var result = await _httpClient.GetFromJsonAsync<HereResponse>(
                url,
                cancellationToken
            );
            var items = result?.Items ?? new List<HereItem>();

            var places = items
                .Where(x =>
                    !string.IsNullOrEmpty(x.Id) &&
                    !string.IsNullOrEmpty(x.Title) &&
                    x.Position is not null)
                .Select(x => new Place(
                    externalId: x.Id!,
                    name: x.Title!,
                    latitude: x.Position!.Lat,
                    longitude: x.Position.Lng,
                    source: "HERE",
                    address: x.Address?.Label,
                    category: x.Categories?.FirstOrDefault()?.Name,
                    openingHours: x.OpeningHours?
                        .SelectMany(h => h.Text ?? new List<string>())
                        .FirstOrDefault()
                ))
                .ToList();

            _logger.LogInformation(
                "HERE search returned {Total} items, {Mapped} valid for query={Query} at ({Lat},{Lon})",
                items.Count, places.Count, query, latitude, longitude);

            return places;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HERE API request failed for query={Query} at ({Lat},{Lon})",
                query, latitude, longitude);
            return [];
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex,
                "HERE API request timed out for query={Query} at ({Lat},{Lon})",
                query, latitude, longitude);
            return [];
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogError(ex,
                "HERE API response parse failed for query={Query} at ({Lat},{Lon})",
                query, latitude, longitude);
            return [];
        }
    }
}
