using Microsoft.EntityFrameworkCore;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _dbContext;
    public UserRepository(UsersDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == id);
    }
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == email);
    }

    public async Task<IReadOnlyList<User>> SearchUsersAsync(
        string? query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var q = _dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            q = q.Where(u => EF.Functions.Like(u.Username, pattern) || EF.Functions.Like(u.Email, pattern));
        }

        return await q.OrderBy(u => u.Username)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }
    public async Task<(IReadOnlyList<User> items, int totalCount)> GetPagedUsersAsync(
        string? query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var q = _dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            q = q.Where(u => EF.Functions.Like(u.Username, pattern) || EF.Functions.Like(u.Email, pattern));
        }

        var total = await q.CountAsync(cancellationToken);
        var pageNum = Math.Max(1, page);
        var size = Math.Clamp(pageSize, 1, 100);

        var items = await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNum - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(User user)
    {
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user != null)
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteRangeAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0) return;
        var users = await _dbContext.Users.Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);
        if (users.Count > 0)
        {
            _dbContext.Users.RemoveRange(users);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}