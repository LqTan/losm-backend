using System.Globalization;
using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.GetDirections;
using Configuration.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;
using Places.Infrastructure.Persistence;
using Search.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class GetDirectionsTool : AgentTool<GetDirectionsToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOsrmRouteService _osrmRouteService;
    private readonly IGeocodingService _geocodingService;
    private readonly PlacesDbContext _db;
    private readonly ILogger<GetDirectionsTool> _logger;

    public GetDirectionsTool(
        IOsrmRouteService osrmRouteService,
        IGeocodingService geocodingService,
        PlacesDbContext db,
        ITuningProvider tuning,
        ILogger<GetDirectionsTool> logger
    ) : base(tuning, "get_directions")
    {
        _osrmRouteService = osrmRouteService;
        _geocodingService = geocodingService;
        _db = db;
        _logger = logger;
    }

    public override string Name => "get_directions";

    public override string Description =>
        "Calculates detailed driving directions and route coordinates between an origin and destination. " +
        "Use this tool when the user asks for directions, route, or travel time from a specific origin " +
        "(e.g. from a group member's location like 'Chợ Bến Thành', 'Bưu điện Trung tâm') to a destination place. " +
        "Do NOT guess or estimate routes without calling this tool.";

    public override bool SuppliesPlaces => false;

    public override ToolDisplay Display => new(
        DisplayName: "Get directions",
        CallingMessage: "Calculating route and travel time...",
        SuccessMessage: "Route found",
        FailedMessage: "No suitable route found",
        PendingConfirmationMessage: "Awaiting confirmation"
    );

    protected override async Task<string> ExecuteAsync(
        GetDirectionsToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(arguments.Origin))
        {
            throw new ArgumentException("Origin is required.", nameof(arguments.Origin));
        }

        if (string.IsNullOrWhiteSpace(arguments.Destination))
        {
            throw new ArgumentException("Destination is required.", nameof(arguments.Destination));
        }

        var originLoc = await ResolveLocationAsync(arguments.Origin, cancellationToken);
        if (originLoc is null)
        {
            return JsonSerializer.Serialize(new
            {
                succeeded = false,
                error = $"Coordinates not found for origin '{arguments.Origin}'."
            }, JsonOptions);
        }

        var destLoc = await ResolveLocationAsync(arguments.Destination, cancellationToken);
        if (destLoc is null)
        {
            return JsonSerializer.Serialize(new
            {
                succeeded = false,
                error = $"Coordinates not found for destination '{arguments.Destination}'."
            }, JsonOptions);
        }

        var route = await _osrmRouteService.GetRouteAsync(
            new GeoCoordinate(originLoc.Value.Lat, originLoc.Value.Lng),
            new GeoCoordinate(destLoc.Value.Lat, destLoc.Value.Lng),
            cancellationToken
        );

        if (route is null || route.Coordinates.Count == 0)
        {
            return JsonSerializer.Serialize(new
            {
                succeeded = false,
                error = $"No route available from '{originLoc.Value.Name}' to '{destLoc.Value.Name}'."
            }, JsonOptions);
        }

        var attachedRoute = new AttachedRoute(
            OriginName: originLoc.Value.Name,
            OriginLatitude: originLoc.Value.Lat,
            OriginLongitude: originLoc.Value.Lng,
            DestinationName: destLoc.Value.Name,
            DestinationLatitude: destLoc.Value.Lat,
            DestinationLongitude: destLoc.Value.Lng,
            MemberName: arguments.MemberName,
            DistanceKm: route.DistanceKm,
            DurationMinutes: route.DurationMinutes,
            Summary: route.Summary,
            Coordinates: route.Coordinates.Select(c => (IReadOnlyList<double>)new[] { c.Latitude, c.Longitude }).ToList()
        );

        return JsonSerializer.Serialize(new
        {
            succeeded = true,
            origin = originLoc.Value.Name,
            destination = destLoc.Value.Name,
            memberName = arguments.MemberName,
            distanceKm = Math.Round(route.DistanceKm, 2),
            durationMinutes = Math.Round(route.DurationMinutes, 1),
            summary = route.Summary,
            route = attachedRoute
        }, JsonOptions);
    }

    private async Task<(double Lat, double Lng, string Name)?> ResolveLocationAsync(
        string locationInput,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(locationInput)) return null;
        var trimmed = locationInput.Trim();

        // 1. Toạ độ dạng "lat, lng" hoặc "lat; lng"
        var parts = trimmed.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
        {
            return (lat, lng, trimmed);
        }

        // 2. Tra theo Guid Id của Place
        if (Guid.TryParse(trimmed, out var placeId))
        {
            var p = await _db.Places.AsNoTracking().FirstOrDefaultAsync(x => x.Id == placeId, ct);
            if (p is not null)
            {
                return (p.Latitude, p.Longitude, p.Name);
            }
        }

        // 3. Tra theo tên trong cơ sở dữ liệu Places
        var dbPlace = await _db.Places.AsNoTracking()
            .Where(p => !p.IsNotSafe && EF.Functions.Like(p.Name, $"%{trimmed}%"))
            .OrderByDescending(p => p.Name.ToLower() == trimmed.ToLower() ? 100 : 50)
            .FirstOrDefaultAsync(ct);

        if (dbPlace is not null)
        {
            return (dbPlace.Latitude, dbPlace.Longitude, dbPlace.Name);
        }

        // 4. Tra cứu qua DbGeocodingService (nội bộ hệ thống)
        var geocoded = await _geocodingService.GeocodeAsync(trimmed, ct);
        if (geocoded is not null)
        {
            return (geocoded.Latitude, geocoded.Longitude, geocoded.Name);
        }

        return null;
    }
}
