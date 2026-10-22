using System.Collections.Concurrent;
using AgentCore.Domain.Entities;

namespace AgentCore.Infrastructure.PromptsManagement;

// Singleton, shared by request scopes. Absolute expiry bounds cross-instance staleness.
public sealed class PromptCache(TimeProvider clock)
{
    private sealed record Entry(string? Content, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    public async Task<string?> GetAsync(string key, Func<Task<PromptRevision?>> load, CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_entries.TryGetValue(key, out var entry) && entry.ExpiresAt > clock.GetUtcNow())
                return entry.Content;
            var revision = await load();
            _entries[key] = new(revision?.Content, clock.GetUtcNow() + Lifetime);
            return revision?.Content;
        }
        finally { gate.Release(); }
    }

    public async Task InvalidateAsync(string key)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try { _entries.TryRemove(key, out _); }
        finally { gate.Release(); }
    }
}

public sealed class PromptProvider(IPromptStore store, PromptCache cache)
{
    public async Task<string> ResolveAsync(string key, string fallback, CancellationToken ct) =>
        await cache.GetAsync(key, () => store.GetAsync(key, ct), ct) ?? fallback;
}
