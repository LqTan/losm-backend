using AgentCore.Domain.Entities;

namespace AgentCore.Application.Abstractions;

public interface ILastSearchContextStore
{
    Task<LastSearchContext?> GetAsync(Guid sessionId, CancellationToken ct);
    Task UpsertAsync(LastSearchContext context, CancellationToken ct);
}
