using Users.Domain.Entities;

namespace Users.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<IReadOnlyList<User>> SearchUsersAsync(string? query, int limit = 20, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<User> items, int totalCount)> GetPagedUsersAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);
}