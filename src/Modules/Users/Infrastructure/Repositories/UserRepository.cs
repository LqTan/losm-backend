using Microsoft.EntityFrameworkCore;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Domain.Enums;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
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

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Username == username);
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

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
            return;

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AnyAsync(
                x => x.Email == email
                    && (excludeId == null || x.Id != excludeId),
                cancellationToken);
    }

    public Task<bool> ExistsByUsernameAsync(
        string username,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AnyAsync(
                x => x.Username == username
                    && (excludeId == null || x.Id != excludeId),
                cancellationToken);
    }

    public Task<bool> ExistsAdministratorAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AnyAsync(
                x => x.Role == UserRole.Administrator,
                cancellationToken);
    }

    public async Task<(
        IReadOnlyList<User> Items,
        int TotalCount)> SearchAsync(
        UserSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = Math.Clamp(criteria.PageSize, 1, 200);

        var query = ApplyFilters(_dbContext.Users.AsQueryable(), criteria);

        query = ApplySort(query, criteria.SortBy, criteria.SortDescending);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<UserDashboardStats> GetDashboardStatsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        var customers = _dbContext.Users
            .Where(x => x.Role == UserRole.Customer);

        var inRange = customers
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to);

        var totalCustomers = await customers.CountAsync(cancellationToken);

        var activeCustomers = await customers.CountAsync(
            x => x.Status == UserStatus.Active,
            cancellationToken);

        var blockedCustomers = await customers.CountAsync(
            x => x.Status == UserStatus.Blocked,
            cancellationToken);

        var newCustomers = await inRange.CountAsync(cancellationToken);

        var loggedInCustomers = await customers.CountAsync(
            x => x.LastLoginAt != null
                && x.LastLoginAt >= from
                && x.LastLoginAt <= to,
            cancellationToken);

        var totalAdministrators = await _dbContext.Users.CountAsync(
            x => x.Role == UserRole.Administrator,
            cancellationToken);

        var rawByDay = await inRange
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byDayLookup = rawByDay.ToDictionary(
            x => DateOnly.FromDateTime(x.Date),
            x => x.Count);

        var registrationsByDay = new List<UserDailyCount>();
        for (var day = from.Date; day.Date <= to.Date; day = day.AddDays(1))
        {
            var key = DateOnly.FromDateTime(day);
            registrationsByDay.Add(new UserDailyCount(
                key,
                byDayLookup.TryGetValue(key, out var count)
                    ? count
                    : 0));
        }

        var byGender = await customers
            .GroupBy(x => x.Gender)
            .Select(g => new
            {
                Gender = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var byStatus = await customers
            .GroupBy(x => x.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var recentCustomers = await customers
            .OrderByDescending(x => x.CreatedAt)
            .Take(10)
            .Select(x => new UserRecentUser(
                x.Id,
                x.FullName,
                x.Username,
                x.Phone,
                x.Gender,
                x.CreatedAt,
                x.LastLoginAt))
            .ToListAsync(cancellationToken);

        return new UserDashboardStats(
            totalCustomers,
            activeCustomers,
            blockedCustomers,
            newCustomers,
            loggedInCustomers,
            totalAdministrators,
            registrationsByDay,
            byGender.Select(x => new UserGroupCount(
                x.Gender?.ToString() ?? "Unspecified",
                x.Count)).ToList(),
            byStatus.Select(x => new UserGroupCount(
                x.Status.ToString(),
                x.Count)).ToList(),
            recentCustomers);
    }

    private static IQueryable<User> ApplyFilters(
        IQueryable<User> query,
        UserSearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            var keyword = criteria.Keyword.Trim();
            query = query.Where(x =>
                x.Username.Contains(keyword)
                || x.Email.Contains(keyword)
                || x.FullName.Contains(keyword)
                || (x.Phone != null && x.Phone.Contains(keyword)));
        }

        if (criteria.Role is not null)
            query = query.Where(x => x.Role == criteria.Role.Value);

        if (criteria.ExcludeRole is not null)
        {
            var excluded = criteria.ExcludeRole.Value;
            query = query.Where(x => x.Role != excluded);
        }

        if (criteria.Status is not null)
            query = query.Where(x => x.Status == criteria.Status.Value);

        if (criteria.Gender is not null)
            query = query.Where(x => x.Gender == criteria.Gender);

        if (criteria.From is not null)
        {
            var from = criteria.From.Value;
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (criteria.To is not null)
        {
            var to = criteria.To.Value;
            query = query.Where(x => x.CreatedAt <= to);
        }

        return query;
    }

    private static IQueryable<User> ApplySort(
        IQueryable<User> query,
        string? sortBy,
        bool descending)
    {
        var ordered = sortBy?.ToLowerInvariant() switch
        {
            "username" => query.OrderBy(x => x.Username),
            "email" => query.OrderBy(x => x.Email),
            "fullname" => query.OrderBy(x => x.FullName),
            "status" => query.OrderBy(x => x.Status),
            "role" => query.OrderBy(x => x.Role),
            "lastloginat" => query.OrderBy(x => x.LastLoginAt),
            "changedat" => query.OrderBy(x => x.ChangedAt),
            _ => query.OrderBy(x => x.CreatedAt)
        };

        return descending ? ordered.Reverse() : ordered;
    }
}
