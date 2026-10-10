using System.Collections.Concurrent;
using Configuration.Application.Abstractions;
using Configuration.Application.Models;

namespace Configuration.Infrastructure;

public sealed class InMemoryConfigurationStore : IConfigurationStore
{
    private readonly ConcurrentDictionary<string, ConfigurationDoc> _docs = new();
    private readonly ConcurrentBag<ConfigurationChange> _history = new();
    private long _version = 1;

    public Task<ConfigurationDoc?> GetAsync(string scope, CancellationToken ct)
    {
        _docs.TryGetValue(scope, out var doc);
        return Task.FromResult(doc);
    }

    public Task<IReadOnlyList<string>> ListScopesAsync(CancellationToken ct)
    {
        IReadOnlyList<string> scopes = _docs.Keys.ToList();
        return Task.FromResult(scopes);
    }

    public Task<UpsertResult> UpsertAsync(
        string scope,
        string module,
        string key,
        string valueJson,
        long? expectedVersion,
        Guid? updatedBy,
        CancellationToken ct)
    {
        _docs.TryGetValue(scope, out var oldDoc);
        if (expectedVersion.HasValue && oldDoc?.Version != expectedVersion.Value)
        {
            return Task.FromResult(new UpsertResult(false, oldDoc?.Version ?? 0, "Version mismatch"));
        }

        var newVersion = Interlocked.Increment(ref _version);
        var doc = new ConfigurationDoc(scope, module, key, valueJson, newVersion, DateTime.UtcNow, updatedBy);
        _docs[scope] = doc;
        _history.Add(new ConfigurationChange(
            scope,
            oldDoc?.ValueJson,
            valueJson,
            oldDoc?.Version ?? 0,
            newVersion,
            DateTime.UtcNow,
            updatedBy,
            oldDoc is null ? "create" : "update"
        ));
        return Task.FromResult(new UpsertResult(true, newVersion, null));
    }

    public Task<DeleteResult> DeleteAsync(
        string scope,
        long expectedVersion,
        Guid? deletedBy,
        CancellationToken ct)
    {
        if (_docs.TryGetValue(scope, out var oldDoc))
        {
            if (oldDoc.Version != expectedVersion)
            {
                return Task.FromResult(new DeleteResult(false, "Version mismatch"));
            }
            _docs.TryRemove(scope, out _);
            var newVersion = Interlocked.Increment(ref _version);
            _history.Add(new ConfigurationChange(
                scope,
                oldDoc.ValueJson,
                string.Empty,
                oldDoc.Version,
                newVersion,
                DateTime.UtcNow,
                deletedBy,
                "delete"
            ));
            return Task.FromResult(new DeleteResult(true, null));
        }

        return Task.FromResult(new DeleteResult(false, "Not found"));
    }

    public Task<IReadOnlyList<ConfigurationChange>> GetHistoryAsync(
        string scope,
        int limit,
        CancellationToken ct)
    {
        IReadOnlyList<ConfigurationChange> list = _history
            .Where(h => h.Scope == scope)
            .OrderByDescending(h => h.NewVersion)
            .Take(limit)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<long> GetCurrentVersionAsync(CancellationToken ct)
    {
        return Task.FromResult(_version);
    }
}
