using System.Collections.Concurrent;
using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;

namespace AgentCore.Tests.TestSupport;

public sealed class InMemoryLastSearchContextStore : ILastSearchContextStore
{
    private readonly ConcurrentDictionary<Guid, LastSearchContext> _store = new();

    public Task<LastSearchContext?> GetAsync(Guid sessionId, CancellationToken ct)
    {
        _store.TryGetValue(sessionId, out var c);
        return Task.FromResult(c);
    }

    public Task UpsertAsync(LastSearchContext context, CancellationToken ct)
    {
        _store[context.SessionId] = context;
        return Task.CompletedTask;
    }
}
