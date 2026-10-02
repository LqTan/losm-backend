namespace Configuration.Application.Models;

public sealed record AgentCoreToolOptions
{
    public string ToolName { get; init; } = string.Empty;
    public double DefaultRadiusKm { get; init; } = 5;
    public int DefaultLimit { get; init; } = 10;
    public int MaxLimit { get; init; } = 20;
}
