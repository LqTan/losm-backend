using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;

namespace AgentCore.Application.Agent.Commands.CreateAgentPlan;

public sealed class CreateAgentPlanHandler
{
    private readonly IAgentPlanner _planner;
    private readonly IAgentPlanRepository _planRepository;
    private readonly IAgentSessionRepository _sessionRepository;

    public CreateAgentPlanHandler(
        IAgentPlanner planner,
        IAgentPlanRepository planRepository,
        IAgentSessionRepository sessionRepository
    )
    {
        _planner = planner;
        _planRepository = planRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<CreateAgentPlanResult> HandleAsync(
        CreateAgentPlanCommand command,
        CancellationToken cancellationToken = default
    )
    {
        IReadOnlyCollection<AgentMessage> conversation =
            Array.Empty<AgentMessage>();

        if (command.SessionId.HasValue)
        {
            var session =
                await _sessionRepository.GetByIdAsync(
                    command.SessionId.Value,
                    cancellationToken
                );

            if (session is null ||
                session.UserId != command.UserId)
            {
                throw new KeyNotFoundException(
                    $"Agent session '{command.SessionId}' was not found."
                );
            }

            conversation = session.Messages;
        }

        var content = await _planner.CreateAsync(
            command.Message,
            command.Latitude,
            command.Longitude,
            conversation,
            cancellationToken
        );

        var plan = new AgentPlan(
            Guid.NewGuid(),
            command.UserId,
            command.SessionId,
            command.Message,
            content,
            command.Latitude,
            command.Longitude
        );

        await _planRepository.AddAsync(
            plan,
            cancellationToken
        );

        return new CreateAgentPlanResult(
            plan.Id,
            plan.SessionId,
            plan.Content,
            plan.Status.ToString()
        );
    }
}
