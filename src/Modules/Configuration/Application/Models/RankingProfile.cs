namespace Configuration.Application.Models;

public sealed record RankingProfile
{
    public string Name { get; init; } = "Default";
    public double Relevance { get; init; } = 0.6;
    public double Distance { get; init; } = 0.3;
    public double Fairness { get; init; } = 0.0;
    public double Rating { get; init; } = 0.1;
}
