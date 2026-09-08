namespace AgentCore.Application.Models;

public sealed record AgentToolExecution(
    string ToolName,
    string Result,
    bool Succeeded
);
