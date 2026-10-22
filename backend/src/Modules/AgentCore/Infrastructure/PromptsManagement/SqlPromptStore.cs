using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.PromptsManagement;

public interface IPromptStore
{
    Task<PromptRevision?> GetAsync(string key, CancellationToken ct);
    Task<IReadOnlyList<PromptRevision>> HistoryAsync(string key, CancellationToken ct);
    Task<PromptRevision?> PublishAsync(string key, string content, long expectedVersion, Guid? userId, CancellationToken ct);
}

public sealed class SqlPromptStore(AgentCoreDbContext db) : IPromptStore
{
    public Task<PromptRevision?> GetAsync(string key, CancellationToken ct) => db.Set<PromptRevision>()
        .AsNoTracking().Where(p => p.Key == key).OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PromptRevision>> HistoryAsync(string key, CancellationToken ct) =>
        await db.Set<PromptRevision>().AsNoTracking().Where(p => p.Key == key)
            .OrderByDescending(p => p.Version).Take(50).ToListAsync(ct);

    public async Task<PromptRevision?> PublishAsync(string key, string content, long expectedVersion, Guid? userId, CancellationToken ct)
    {
        var current = await GetAsync(key, ct);
        if (expectedVersion < 0 || (current?.Version ?? 0) != expectedVersion) return null;
        var revision = new PromptRevision
        {
            Key = key, Content = content, Version = checked(expectedVersion + 1),
            ChangedAt = DateTime.UtcNow, ChangedBy = userId
        };
        db.Add(revision);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // The composite primary key also protects concurrent first publications across servers.
            db.Entry(revision).State = EntityState.Detached;
            return null;
        }
        return revision;
    }
}
