namespace Transit.Domain.Models;

public sealed record NetworkStop(Guid Id, string ExternalId, string Name, string Address,
    double Latitude, double Longitude, string Mode, bool IsNotSafe);
public sealed record NetworkRoute(Guid Id, int Number, string Name, string Mode,
    decimal? ReferenceFare, string FareDescription);
public sealed record NetworkCall(Guid StopId, int Sequence);
public sealed record NetworkPattern(Guid Id, Guid RouteId, int Direction, string Name,
    int? FirstMinute, int? LastMinute, int? HeadwayMin, int? HeadwayMax,
    string ServiceDays, IReadOnlyList<NetworkCall> Calls);
public sealed record TransitNetwork(IReadOnlyList<NetworkStop> Stops,
    IReadOnlyList<NetworkRoute> Routes, IReadOnlyList<NetworkPattern> Patterns);
