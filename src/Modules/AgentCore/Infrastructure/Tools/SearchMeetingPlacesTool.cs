using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SearchMeetingPlaces;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;
using Search.Application.Abstractions;
using Search.Application.Search.Queries.SearchMeetingPlaces;

namespace AgentCore.Infrastructure.Tools;

public sealed class SearchMeetingPlacesTool
    : AgentTool<SearchMeetingPlacesToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SearchMeetingPlacesHandler _handler;
    private readonly IGeocodingService _geocodingService;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;
    private readonly ILogger<SearchMeetingPlacesTool> _logger;

    public SearchMeetingPlacesTool(
        SearchMeetingPlacesHandler handler,
        IGeocodingService geocodingService,
        Configuration.Application.Abstractions.ITuningProvider tuning,
        ILogger<SearchMeetingPlacesTool> logger)
        : base(tuning, "search_meeting_places")
    {
        _handler = handler;
        _geocodingService = geocodingService;
        _tuning = tuning;
        _logger = logger;
    }

    public override string Name => "search_meeting_places";

    public override string Description =>
        "Search for places convenient for a multi-person meeting. " +
        "Accepts multiple origins; each origin may be a pair of latitude/longitude, " +
        "or a place name that will be geocoded automatically. " +
        "If a named origin cannot be geocoded, the tool returns a structured " +
        "'geocode_failed' error so you can ask the user to clarify. " +
        "Use this whenever the user mentions multiple participants " +
        "in different locations who need a shared meeting place.";

    public override bool SuppliesPlaces => true;

    protected override async Task<string> ExecuteAsync(
        SearchMeetingPlacesToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(arguments.Query))
        {
            throw new ArgumentException("Query is required.", nameof(arguments.Query));
        }

        if (arguments.Origins is null || arguments.Origins.Count == 0)
        {
            throw new ArgumentException(
                "At least one origin is required.",
                nameof(arguments.Origins));
        }

        var resolvedOrigins = new List<ResolvedOrigin>(arguments.Origins.Count);
        var failures = new List<GeocodeFailure>();

        foreach (var origin in arguments.Origins)
        {
            var resolved = await TryResolveAsync(origin, cancellationToken);
            if (resolved is null)
            {
                failures.Add(new GeocodeFailure(
                    origin.Name ?? "(unknown)",
                    origin.Latitude,
                    origin.Longitude));
                continue;
            }
            resolvedOrigins.Add(resolved);
        }

        if (failures.Count > 0)
        {
            var payload = JsonSerializer.Serialize(new
            {
                error = "geocode_failed",
                message = "One or more origins could not be geocoded. " +
                          "Ask the user to provide coordinates or a more specific name.",
                failures = failures
            }, JsonOptions);

            throw new InvalidOperationException(payload);
        }

        if (resolvedOrigins.Count == 0)
        {
            throw new ArgumentException(
                "No valid origins after resolution.",
                nameof(arguments.Origins));
        }

        var query = new SearchMeetingPlacesQuery(
            arguments.Query,
            resolvedOrigins
                .Select(o => new SearchMeetingPlaceOrigin(
                    o.Name,
                    o.Latitude,
                    o.Longitude))
                .ToList(),
            arguments.RadiusKm,
            arguments.TopK);

        var result = await _handler.HandleAsync(query, cancellationToken);

        var dto = new
        {
            query = arguments.Query,
            centroid = new
            {
                latitude = result.CentroidLatitude,
                longitude = result.CentroidLongitude
            },
            searchRadiusKm = result.SearchRadiusKm,
            profile = result.Profile,
            origins = resolvedOrigins.Select(o => new
            {
                name = o.Name,
                latitude = o.Latitude,
                longitude = o.Longitude,
                displayName = o.DisplayName
            }).ToList(),
            results = result.Results.Select(r => new
            {
                placeId = r.PlaceId,
                name = r.Name,
                address = r.Address,
                latitude = r.Latitude,
                longitude = r.Longitude,
                category = r.Category,
                openingHours = r.OpeningHours,
                distanceKmByOrigin = r.DistanceKmByOrigin,
                averageDistanceKm = r.AverageDistanceKm,
                maxDistanceKm = r.MaxDistanceKm,
                spreadKm = r.SpreadKm,
                relevanceScore = r.RelevanceScore,
                distanceScore = r.DistanceScore,
                fairnessScore = r.FairnessScore,
                finalScore = r.FinalScore
            }).ToList()
        };

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    private async Task<ResolvedOrigin?> TryResolveAsync(
        SearchMeetingPlaceOriginArgument origin,
        CancellationToken cancellationToken)
    {
        var hasLat = origin.Latitude.HasValue;
        var hasLon = origin.Longitude.HasValue;

        if (hasLat && hasLon)
        {
            return new ResolvedOrigin(
                origin.Name,
                origin.Latitude!.Value,
                origin.Longitude!.Value,
                null);
        }

        if (string.IsNullOrWhiteSpace(origin.Name))
        {
            return null;
        }

        var geocoded = await _geocodingService.GeocodeAsync(
            origin.Name,
            cancellationToken);

        if (geocoded is null)
        {
            _logger.LogInformation(
                "SearchMeetingPlacesTool: geocode failed for origin '{Origin}'",
                origin.Name);
            return null;
        }

        return new ResolvedOrigin(
            geocoded.Name,
            geocoded.Latitude,
            geocoded.Longitude,
            geocoded.DisplayName);
    }

    private sealed record ResolvedOrigin(
        string? Name,
        double Latitude,
        double Longitude,
        string? DisplayName);

    private sealed record GeocodeFailure(
        string Name,
        double? Latitude,
        double? Longitude);
}
