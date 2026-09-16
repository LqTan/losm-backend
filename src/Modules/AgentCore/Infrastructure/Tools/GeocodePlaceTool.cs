using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.GeocodePlace;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class GeocodePlaceTool
    : AgentTool<GeocodePlaceToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IGeocodingService _geocodingService;
    private readonly Configuration.Application.Abstractions.ITuningProvider _tuning;
    private readonly ILogger<GeocodePlaceTool> _logger;

    public GeocodePlaceTool(
        IGeocodingService geocodingService,
        Configuration.Application.Abstractions.ITuningProvider tuning,
        ILogger<GeocodePlaceTool> logger)
        : base(tuning, "geocode_place")
    {
        _geocodingService = geocodingService;
        _tuning = tuning;
        _logger = logger;
    }

    public override string Name => "geocode_place";

    public override string Description =>
        "Resolve a place name (district, city, landmark, address) to latitude/longitude. " +
        "Returns a single best match from OpenStreetMap Nominatim. " +
        "Use this when the user mentions a place by name and you need coordinates " +
        "(e.g. for multi-origin meeting search). " +
        "If the query cannot be resolved, the result will indicate 'notFound': true " +
        "so you can ask the user to clarify.";

    protected override async Task<string> ExecuteAsync(
        GeocodePlaceToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(arguments.Query))
        {
            throw new ArgumentException("Query is required.", nameof(arguments.Query));
        }

        var geocoded = await _geocodingService.GeocodeAsync(
            arguments.Query,
            cancellationToken);

        if (geocoded is null)
        {
            _logger.LogInformation(
                "GeocodePlaceTool: no match for query={Query}", arguments.Query);

            return JsonSerializer.Serialize(new
            {
                query = arguments.Query,
                notFound = true,
                message = $"Could not resolve '{arguments.Query}'. " +
                          "Ask the user for a more specific name or provide coordinates."
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            query = arguments.Query,
            notFound = false,
            name = geocoded.Name,
            displayName = geocoded.DisplayName,
            latitude = geocoded.Latitude,
            longitude = geocoded.Longitude,
            category = geocoded.Category,
            type = geocoded.Type,
            boundingBox = geocoded.Box is null ? null : new
            {
                minLatitude = geocoded.Box.MinLatitude,
                maxLatitude = geocoded.Box.MaxLatitude,
                minLongitude = geocoded.Box.MinLongitude,
                maxLongitude = geocoded.Box.MaxLongitude
            }
        }, JsonOptions);
    }
}
