using Users.Domain.Entities;
using Users.Domain.Enums;

namespace Users.Application.Abstractions;

public sealed record UserSearchCriteria(
    string? Keyword = null,
    UserRole? Role = null,
    UserStatus? Status = null,
    UserGender? Gender = null,
    DateTime? From = null,
    DateTime? To = null,
    string SortBy = "createdAt",
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20,
    UserRole? ExcludeRole = null
);

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByUsernameAsync(
        string username,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByPhoneAsync(
        string phone,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAdministratorAsync(
        CancellationToken cancellationToken = default);
    Task<(
        IReadOnlyList<User> Items,
        int TotalCount)> SearchAsync(
        UserSearchCriteria criteria,
        CancellationToken cancellationToken = default);
    Task<UserDashboardStats> GetDashboardStatsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
