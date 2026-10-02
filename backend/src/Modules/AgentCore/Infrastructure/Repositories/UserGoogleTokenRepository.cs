using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentCore.Infrastructure.Repositories;

public sealed class UserGoogleTokenRepository : IUserGoogleTokenRepository
{
    private readonly AgentCoreDbContext _db;

    public UserGoogleTokenRepository(AgentCoreDbContext db)
    {
        _db = db;
    }

    public Task<UserGoogleToken?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _db.UserGoogleTokens
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task<UserGoogleToken?> GetByGoogleSubAsync(
        string googleSub,
        CancellationToken cancellationToken = default)
    {
        return _db.UserGoogleTokens
            .FirstOrDefaultAsync(x => x.GoogleSub == googleSub, cancellationToken);
    }

    public async Task AddAsync(
        UserGoogleToken token,
        CancellationToken cancellationToken = default)
    {
        await _db.UserGoogleTokens.AddAsync(token, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        UserGoogleToken token,
        CancellationToken cancellationToken = default)
    {
        _db.UserGoogleTokens.Update(token);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserGoogleTokens
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            _db.UserGoogleTokens.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}