using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.Stores;

public sealed class EfPendingActionStore : IPendingActionStore
{
    private readonly AgentCoreDbContext _db;

    public EfPendingActionStore(AgentCoreDbContext db)
    {
        _db = db;
    }

    public async Task<PendingAgentAction> AddAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        await _db.PendingAgentActions.AddAsync(action, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return action;
    }

    public async Task<PendingAgentAction?> GetAsync(
        Guid actionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PendingAgentActions
            .FirstOrDefaultAsync(x => x.Id == actionId, cancellationToken);
    }

    public async Task<PendingAgentAction?> GetByConfirmationAsync(
        Guid confirmationId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PendingAgentActions
            .FirstOrDefaultAsync(x => x.ConfirmationId == confirmationId, cancellationToken);
    }

    public async Task<PendingAgentAction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _db.PendingAgentActions
            .FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingAgentAction>> GetBySessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PendingAgentActions
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingAgentAction>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PendingAgentActions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        PendingAgentAction action,
        CancellationToken cancellationToken = default)
    {
        _db.PendingAgentActions.Update(action);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
