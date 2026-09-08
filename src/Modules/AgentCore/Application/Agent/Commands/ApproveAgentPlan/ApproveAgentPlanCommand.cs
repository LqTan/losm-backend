namespace AgentCore.Application.Agent.Commands.ApproveAgentPlan;

public sealed record ApproveAgentPlanCommand(
    Guid PlanId,
    Guid UserId
);
