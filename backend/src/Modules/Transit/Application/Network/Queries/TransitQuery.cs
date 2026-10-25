namespace Transit.Application.Network.Queries;

public sealed record TransitLocation(string? Name = null, double? Latitude = null, double? Longitude = null);
public sealed record TransitQuery(string Kind, string? Mode = null, string? Query = null,
    int? RouteNumber = null, TransitLocation? Origin = null, TransitLocation? Destination = null,
    double RadiusMeters = 500, int Limit = 10);
public sealed record TransitStopResult(Guid Id, string Name, string Address, double Latitude,
    double Longitude, string Mode, double? DistanceMeters, IReadOnlyList<int> RouteNumbers);
public sealed record TransitRouteResult(Guid Id, int Number, string Name, string Mode, Guid PatternId,
    int Direction, string DirectionName, int? FirstMinute, int? LastMinute, int? HeadwayMin,
    int? HeadwayMax, string ServiceDays, decimal? ReferenceFare, string FareDescription,
    bool HasExcludedStops, IReadOnlyList<TransitStopResult> Stops);
public sealed record TransitJourneyResult(
    TransitRouteResult Route,
    TransitStopResult BoardingStop,
    TransitStopResult AlightingStop,
    int StopCount,
    double AccessDistanceMeters,
    double EgressDistanceMeters,
    double Score = 0,
    string RankingMethod = "cradis-dn4");

public sealed record TransitQueryResult(
    string Kind,
    string Status,
    string Message,
    IReadOnlyList<string> SupportedAreas,
    IReadOnlyList<TransitStopResult> Stops,
    IReadOnlyList<TransitRouteResult> Routes,
    IReadOnlyList<TransitJourneyResult> Journeys,
    bool RequiresLocationConfirmation = false,
    string DistanceKind = "straight_line",
    bool IsRealtime = false,
    TransitLocation? Origin = null,
    TransitLocation? Destination = null,
    string RankingMethod = "cradis-dn4");
