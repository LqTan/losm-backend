using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Agent.Commands.ApproveAgentPlan;

public sealed class ApproveAgentPlanHandler
{
    private readonly IAgentPlanRepository _planRepository;
    private readonly IAgentRunner _agentRunner;

    public ApproveAgentPlanHandler(
        IAgentPlanRepository planRepository,
        IAgentRunner agentRunner
    )
    {
        _planRepository = planRepository;
        _agentRunner = agentRunner;
    }

    public async Task<ApproveAgentPlanResult> HandleAsync(
        ApproveAgentPlanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var plan = await _planRepository.GetByIdAsync(
            command.PlanId,
            cancellationToken
        );

        if (plan is null ||
            plan.UserId != command.UserId)
        {
            throw new KeyNotFoundException(
                $"Agent plan '{command.PlanId}' was not found."
            );
        }

        plan.Approve();

        await _planRepository.UpdateAsync(
            plan,
            cancellationToken
        );

        var result = await _agentRunner.RunApprovedPlanAsync(
            plan.UserInput,
            plan.Latitude,
            plan.Longitude,
            plan.SessionId,
            plan.UserId,
            plan.Content,
            cancellationToken
        );

        plan.Complete(result.SessionId);

        await _planRepository.UpdateAsync(
            plan,
            cancellationToken
        );

        return new ApproveAgentPlanResult(
            plan.Id,
            result.SessionId,
            result.Answer,
            plan.Status.ToString()
        );
    }
}
