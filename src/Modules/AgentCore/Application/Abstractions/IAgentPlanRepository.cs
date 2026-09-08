using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface IAgentPlanRepository
{
    Task<AgentPlan?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(
        AgentPlan plan,
        CancellationToken cancellationToken = default
    );
    Task UpdateAsync(
        AgentPlan plan,
        CancellationToken cancellationToken = default
    );
}
