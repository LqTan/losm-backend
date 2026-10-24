using System.ComponentModel;
using System.Text.Json;
using AgentCore.Application.Abstractions;
using Transit.Application.Network.Queries;

namespace AgentCore.Application.Tools.Transit;

public sealed record SearchTransitStopsArguments(
    [property: Description("Stop name or address. Omit when searching near a location.")] string? Query = null,
    [property: Description("A place/stop name or explicit coordinates from the user. Never invent coordinates.")] TransitLocation? Near = null,
    [property: Description("Bus or Metro; omit for both modes.")] string? Mode = null,
    double RadiusMeters = 500, int Limit = 10);
public sealed record GetTransitRoutesArguments(int? RouteNumber = null, string? Query = null, string? Mode = null, int Limit = 10);
public sealed record FindTransitJourneyArguments(TransitLocation? Destination = null, TransitLocation? Origin = null,
    string? Mode = null, double RadiusMeters = 500, int Limit = 5);

public sealed class SearchTransitStopsTool(TransitQueryHandler handler, IAgentExecutionContext context) : AgentTool<SearchTransitStopsArguments>
{
    public override string Name => "search_transit_stops";
    public override string Description => "Find bus stops or metro stations by name/address or near a location using published internal transit data. Returns supported districts, stop coordinates and serving route numbers. No external geocoding. Distances are straight-line, not walking routes. Missing location must be clarified.";
    public override ToolDisplay Display => new("Find transit stops", "Searching published stops...", "Transit stop search completed", "Transit stop search failed", "Awaiting confirmation");
    protected override async Task<string> ExecuteAsync(SearchTransitStopsArguments a, CancellationToken ct) =>
        JsonSerializer.Serialize(await handler.HandleAsync(new("stops", a.Mode, a.Query, Origin: a.Near ??
            (string.IsNullOrWhiteSpace(a.Query) ? TransitToolLocation.FromContext(context) : null), RadiusMeters: a.RadiusMeters, Limit: a.Limit), ct), TransitToolLocation.Json);
}
public sealed class GetTransitRoutesTool(TransitQueryHandler handler) : AgentTool<GetTransitRoutesArguments>
{
    public override string Name => "get_transit_routes";
    public override string Description => "Look up a bus/metro line by numeric route number or name. Returns directions, ordered safe stops in supported districts, terminal service windows and reference fares. Route number 01 is integer 1. Excluded stops mean the displayed list is partial; never infer a journey from a partial list.";
    public override ToolDisplay Display => new("Look up transit routes", "Reading published routes...", "Transit routes loaded", "Transit route lookup failed", "Awaiting confirmation");
    protected override async Task<string> ExecuteAsync(GetTransitRoutesArguments a, CancellationToken ct) =>
        JsonSerializer.Serialize(await handler.HandleAsync(new("routes", a.Mode, a.Query, a.RouteNumber, Limit: a.Limit), ct), TransitToolLocation.Json);
}
public sealed class FindTransitJourneyTool(TransitQueryHandler handler, IAgentExecutionContext context) : AgentTool<FindTransitJourneyArguments>
{
    public override string Name => "find_transit_journey";
    public override string Description => "Find direct, zero-transfer bus/metro connections from an origin to a destination. Supports place/landmark names or coordinates for Origin and Destination (e.g. Origin: {\"name\": \"Nhà thờ Đức Bà\"}, Destination: {\"name\": \"Phố đi bộ Nguyễn Huệ\"}). Uses actual line directions and stop sequences, excludes invalid stops and segments outside active districts. Returns boarding/alighting stops, number of stops and straight-line access distances. Cannot calculate transfers, live arrivals, travel duration or confirmed walking paths. Never substitute driving directions for transit.";
    public override ToolDisplay Display => new("Find transit journeys", "Checking direct transit connections...", "Transit journey search completed", "Transit journey search failed", "Awaiting confirmation");
    protected override async Task<string> ExecuteAsync(FindTransitJourneyArguments a, CancellationToken ct) =>
        JsonSerializer.Serialize(await handler.HandleAsync(new("journeys", a.Mode, Origin: a.Origin ?? TransitToolLocation.FromContext(context),
            Destination: a.Destination, RadiusMeters: a.RadiusMeters, Limit: a.Limit), ct), TransitToolLocation.Json);
}
internal static class TransitToolLocation
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static TransitLocation? FromContext(IAgentExecutionContext context) =>
        double.IsFinite(context.Latitude) && double.IsFinite(context.Longitude) &&
        context.Latitude is >= -90 and <= 90 && context.Longitude is >= -180 and <= 180 &&
        (context.Latitude != 0 || context.Longitude != 0)
            ? new(Latitude: context.Latitude, Longitude: context.Longitude) : null;
}
