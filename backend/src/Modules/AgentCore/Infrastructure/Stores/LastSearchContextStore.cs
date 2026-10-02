using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.Stores;

public sealed class LastSearchContextStore : ILastSearchContextStore
{
    private readonly AgentCoreDbContext _db;

    public LastSearchContextStore(AgentCoreDbContext db)
    {
        _db = db;
    }

    public async Task<LastSearchContext?> GetAsync(Guid sessionId, CancellationToken ct)
    {
        return await _db.LastSearchContexts
            .FirstOrDefaultAsync(x => x.SessionId == sessionId, ct);
    }

    public async Task UpsertAsync(LastSearchContext context, CancellationToken ct)
    {
        var existing = await _db.LastSearchContexts
            .FirstOrDefaultAsync(x => x.SessionId == context.SessionId, ct);

        if (existing is null)
        {
            await _db.LastSearchContexts.AddAsync(context, ct);
        }
        else
        {
            existing.Update(
                context.Query,
                context.CenterLatitude,
                context.CenterLongitude,
                context.RadiusKm,
                context.ResultPlaceIdsJson,
                context.FiltersJson);
            _db.LastSearchContexts.Update(existing);
        }

        await _db.SaveChangesAsync(ct);
    }
}
