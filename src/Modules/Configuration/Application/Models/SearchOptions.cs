namespace Configuration.Application.Models;

public sealed record SearchOptions
{
    public int CandidateLimit { get; init; } = 20;
    public int TopK { get; init; } = 5;
    public double DefaultRadiusKm { get; init; } = 5;
    public int HereTimeoutSeconds { get; init; } = 5;
}
