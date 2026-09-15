namespace Configuration.Application.Models;

public sealed record RankingProfile
{
    public string Name { get; init; } = "Default";
    public double Relevance { get; init; } = 2.0 / 3.0;
    public double Distance { get; init; } = 1.0 / 3.0;
    public double Fairness { get; init; } = 0.0;
}
