using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface IPendingActionStore
{
    Task<PendingAgentAction> AddAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default);

    Task<PendingAgentAction?> GetAsync(
        Guid actionId,
        CancellationToken cancellationToken = default);

    Task<PendingAgentAction?> GetByConfirmationAsync(
        Guid confirmationId,
        CancellationToken cancellationToken = default);

    Task<PendingAgentAction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingAgentAction>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingAgentAction>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default);
}
