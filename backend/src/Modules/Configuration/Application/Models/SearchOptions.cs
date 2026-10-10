namespace Configuration.Application.Models;

public sealed record SearchOptions
{
    public int CandidateLimit { get; init; } = 50;
    public int TopK { get; init; } = 20;
    public double DefaultRadiusKm { get; init; } = 5;
}
