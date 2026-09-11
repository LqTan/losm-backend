using System.Collections.Concurrent;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;

namespace AgentCore.Infrastructure.Stores;

public sealed class InMemoryPendingActionStore : IPendingActionStore
{
    private readonly ConcurrentDictionary<Guid, PendingAgentAction> _actions =
        new();

    public PendingAgentAction Add(PendingAgentAction action)
    {
        _actions[action.Id] = action;
        return action;
    }

    public IReadOnlyList<PendingAgentAction> GetBySession(Guid sessionId)
    {
        return _actions.Values
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.CreatedAt)
            .ToList();
    }

    public PendingAgentAction? Remove(Guid actionId)
    {
        if (_actions.TryRemove(actionId, out var action))
        {
            return action;
        }
        return null;
    }
}
