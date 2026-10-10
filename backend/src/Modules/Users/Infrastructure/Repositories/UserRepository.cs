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
}