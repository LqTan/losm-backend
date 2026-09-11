namespace AgentCore.Application.Models;

public sealed record AgentActivityStep(
    int Order,
    AgentActivityStepKind Kind,
    string? ToolName,
    string? Summary,
    bool Succeeded
);

public enum AgentActivityStepKind
{
    ToolCall = 0,
    ToolResult = 1,
    ModelResponse = 2,
    Finalize = 3,
    PendingAction = 4
}
