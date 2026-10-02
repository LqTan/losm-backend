using Configuration.Application.Abstractions;
using Configuration.Application.Models;

namespace AgentCore.Tests.TestSupport;

public sealed class ConfigurationStub : ITuningProvider
{
    public Task<SearchOptions> GetSearchOptionsAsync(CancellationToken ct)
        => Task.FromResult(new SearchOptions());

    public Task<RankingProfile> GetRankingProfileAsync(string name, CancellationToken ct)
        => Task.FromResult(new RankingProfile { Name = name });

    public Task<AgentCoreRuntimeOptions> GetRuntimeAsync(CancellationToken ct)
        => Task.FromResult(new AgentCoreRuntimeOptions());

    public Task<AgentCoreToolOptions> GetToolOptionsAsync(string toolName, CancellationToken ct)
        => Task.FromResult(new AgentCoreToolOptions { ToolName = toolName });

    public Task<IReadOnlyDictionary<string, ToolDisplayOptions>> GetToolDisplaysAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, ToolDisplayOptions>>(
            new Dictionary<string, ToolDisplayOptions>());

    public void InvalidateAll() { }
    public void InvalidateScope(string scope) { }
}
