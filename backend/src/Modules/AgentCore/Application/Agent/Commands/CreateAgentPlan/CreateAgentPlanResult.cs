namespace AgentCore.Application.Agent.Commands.CreateAgentPlan;

public sealed record CreateAgentPlanResult(
    Guid PlanId,
    Guid? SessionId,
    string Plan,
    string Status
);
