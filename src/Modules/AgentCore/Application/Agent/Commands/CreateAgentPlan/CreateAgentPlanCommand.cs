namespace AgentCore.Application.Agent.Commands.CreateAgentPlan;

public sealed record CreateAgentPlanCommand(
    string Message,
    double Latitude,
    double Longitude,
    Guid? SessionId,
    Guid UserId
);
