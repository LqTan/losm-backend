namespace AgentCore.Presentation.Contracts;

public sealed record ExecuteAgentRequest(
    string Message,
    double Latitude,
    double Longitude,
    Guid? SessionId
);
