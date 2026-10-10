using Users.Domain.Entities;

namespace Users.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<IReadOnlyList<User>> SearchUsersAsync(string? query, int limit = 20, CancellationToken cancellationToken = default);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}