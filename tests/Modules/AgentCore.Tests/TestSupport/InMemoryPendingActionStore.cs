using System.Collections.Concurrent;
using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;

namespace AgentCore.Tests.TestSupport;

public sealed class InMemoryPendingActionStore : IPendingActionStore
{
    private readonly ConcurrentDictionary<Guid, PendingAgentAction> _actions = new();

    public Task<PendingAgentAction> AddAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        _actions[action.Id] = action;
        return Task.FromResult(action);
    }

    public Task<PendingAgentAction?> GetAsync(
        Guid actionId,
        CancellationToken cancellationToken = default)
    {
        _actions.TryGetValue(actionId, out var a);
        return Task.FromResult(a);
    }

    public Task<PendingAgentAction?> GetByConfirmationAsync(
        Guid confirmationId,
        CancellationToken cancellationToken = default)
    {
        var found = _actions.Values
            .FirstOrDefault(a => a.ConfirmationId == confirmationId);
        return Task.FromResult(found);
    }

    public Task<PendingAgentAction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var found = _actions.Values
            .FirstOrDefault(a => a.IdempotencyKey == idempotencyKey);
        return Task.FromResult(found);
    }

    public Task<IReadOnlyList<PendingAgentAction>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingAgentAction> result = _actions.Values
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        _actions[action.Id] = action;
        return Task.CompletedTask;
    }
}
