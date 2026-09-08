namespace AgentCore.Application.Models;

public sealed record AgentModelResponse(
    AgentModelMessage AssistantMessage,
    IReadOnlyList<AgentModelToolCall> ToolCalls
)
{
    public bool HasToolCalls => ToolCalls.Count > 0;
}
