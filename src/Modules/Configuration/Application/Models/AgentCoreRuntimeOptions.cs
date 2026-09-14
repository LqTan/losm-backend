namespace Configuration.Application.Models;

public sealed record AgentCoreRuntimeOptions
{
    public int MaxSteps { get; init; } = 8;
    public int ToolNameMatchLimit { get; init; } = 5;
    public int ModelTimeoutSeconds { get; init; } = 60;
}
