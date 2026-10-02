namespace AgentCore.Application.Agent.Commands.ExecuteAgent;

public sealed record ExecuteAgentCommand(
    string Message,
    double Latitude,
    double Longitude,
    Guid? SessionId,
    Guid UserId
);
