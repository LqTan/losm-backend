namespace AgentCore.Application.Agent.Commands.ApproveAgentPlan;

public sealed record ApproveAgentPlanResult(
    Guid PlanId,
    Guid SessionId,
    string Answer,
    string Status
);
