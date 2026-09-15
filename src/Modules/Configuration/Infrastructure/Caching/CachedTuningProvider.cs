using System.Collections.Concurrent;
using System.Text.Json;
using Configuration.Application.Abstractions;
using Configuration.Application.Models;
using Microsoft.Extensions.Logging;

namespace Configuration.Infrastructure.Caching;

public sealed class CachedTuningProvider : ITuningProvider
{
    private readonly Application.Abstractions.IConfigurationStore _store;
    private readonly ILogger<CachedTuningProvider> _logger;
    private readonly TimeSpan _maxAge = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private long _lastSeenVersion;
    private DateTime _lastRefreshCheck = DateTime.MinValue;
    private readonly TimeSpan _versionCheckInterval = TimeSpan.FromSeconds(10);

    private sealed record CacheEntry(long Version, DateTime LoadedAt, string ValueJson);

    public CachedTuningProvider(
        Application.Abstractions.IConfigurationStore store,
        ILogger<CachedTuningProvider> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<SearchOptions> GetSearchOptionsAsync(CancellationToken ct)
    {
        var json = await GetOrLoadAsync("Search.Options", ct);
        return DeserializeOrDefault<SearchOptions>(json) ?? new SearchOptions();
    }

    public async Task<RankingProfile> GetRankingProfileAsync(string name, CancellationToken ct)
    {
        var scope = $"Search.Ranking.{name}";
        var json = await GetOrLoadAsync(scope, ct);
        var profile = DeserializeOrDefault<RankingProfile>(json)
                      ?? DefaultProfileFor(name);

        ValidateWeights(scope, profile);
        return profile;
    }

    public async Task<AgentCoreRuntimeOptions> GetRuntimeAsync(CancellationToken ct)
    {
        var json = await GetOrLoadAsync("AgentCore.Runtime", ct);
        return DeserializeOrDefault<AgentCoreRuntimeOptions>(json) ??
               new AgentCoreRuntimeOptions();
    }

    public async Task<AgentCoreToolOptions> GetToolOptionsAsync(string toolName, CancellationToken ct)
    {
        var scope = $"AgentCore.Tools.{toolName}";
        var json = await GetOrLoadAsync(scope, ct);
        return DeserializeOrDefault<AgentCoreToolOptions>(json) ??
               new AgentCoreToolOptions { ToolName = toolName };
    }

    public async Task<IReadOnlyDictionary<string, ToolDisplayOptions>> GetToolDisplaysAsync(CancellationToken ct)
    {
        var json = await GetOrLoadAsync("AgentCore.ToolDisplays", ct);
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, ToolDisplayOptions>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ToolDisplayOptions>>(json)
                   ?? new Dictionary<string, ToolDisplayOptions>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize tool displays");
            return new Dictionary<string, ToolDisplayOptions>();
        }
    }

    public void InvalidateAll()
    {
        _cache.Clear();
        _logger.LogInformation("Tuning cache invalidated");
    }

    public void InvalidateScope(string scope)
    {
        _cache.TryRemove(scope, out _);
    }

    public async Task PollForChangesAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastRefreshCheck < _versionCheckInterval) return;
        _lastRefreshCheck = DateTime.UtcNow;

        try
        {
            var version = await _store.GetCurrentVersionAsync(ct);
            if (version > _lastSeenVersion)
            {
                _lastSeenVersion = version;
                InvalidateAll();
                _logger.LogInformation("Config changed in ES, cache invalidated (v={Version})", version);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Config version poll failed");
        }
    }

    private static RankingProfile DefaultProfileFor(string name)
    {
        if (name == "Meeting")
        {
            return new RankingProfile
            {
                Name = "Meeting",
                Relevance = 5.0 / 9.0,
                Distance = 2.0 / 9.0,
                Fairness = 2.0 / 9.0
            };
        }

        return name == "Default"
            ? new RankingProfile()
            : new RankingProfile { Name = name };
    }

    private void ValidateWeights(string scope, RankingProfile profile)
    {
        var sum = profile.Relevance + profile.Distance + profile.Fairness;
        const double tolerance = 0.001;
        if (Math.Abs(sum - 1.0) > tolerance)
        {
            _logger.LogWarning(
                "Ranking weights for {Scope} sum to {Sum} (expected 1.0). " +
                "Relevance={Relevance} Distance={Distance} Fairness={Fairness}",
                scope, sum,
                profile.Relevance, profile.Distance, profile.Fairness);
        }
    }

    private async Task<string> GetOrLoadAsync(string scope, CancellationToken ct)
    {
        if (_cache.TryGetValue(scope, out var entry) &&
            DateTime.UtcNow - entry.LoadedAt < _maxAge)
        {
            return entry.ValueJson;
        }

        var doc = await _store.GetAsync(scope, ct);
        if (doc is null) return string.Empty;

        _cache[scope] = new CacheEntry(doc.Version, DateTime.UtcNow, doc.ValueJson);
        return doc.ValueJson;
    }

    private T? DeserializeOrDefault<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize config to {Type}", typeof(T).Name);
            return null;
        }
    }
}
