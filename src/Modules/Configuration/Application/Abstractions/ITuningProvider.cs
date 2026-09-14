using Configuration.Application.Models;

namespace Configuration.Application.Abstractions;

public interface ITuningProvider
{
    Task<SearchOptions> GetSearchOptionsAsync(CancellationToken ct);
    Task<RankingProfile> GetRankingProfileAsync(string name, CancellationToken ct);
    Task<AgentCoreRuntimeOptions> GetRuntimeAsync(CancellationToken ct);
    Task<AgentCoreToolOptions> GetToolOptionsAsync(string toolName, CancellationToken ct);
    Task<IReadOnlyDictionary<string, ToolDisplayOptions>> GetToolDisplaysAsync(CancellationToken ct);

    void InvalidateAll();
    void InvalidateScope(string scope);
}
