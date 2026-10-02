namespace AgentCore.Presentation.Contracts;

public sealed record CreateAgentPlanRequest(
    string Message,
    double Latitude,
    double Longitude,
    Guid? SessionId
);
