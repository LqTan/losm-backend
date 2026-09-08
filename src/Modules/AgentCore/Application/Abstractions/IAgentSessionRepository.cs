using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface IAgentSessionRepository
{
    Task<AgentSession?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(
        AgentSession session,
        CancellationToken cancellationToken = default
    );

    Task UpdateAsync(
        AgentSession session,
        CancellationToken cancellationToken = default
    );
}
