using Users.Domain.Enums;

namespace Users.Application.Abstractions;

public sealed record UserDashboardStats(
    int TotalCustomers,
    int ActiveCustomers,
    int BlockedCustomers,
    int NewCustomers,
    int LoggedInCustomers,
    int TotalAdministrators,
    IReadOnlyList<UserDailyCount> RegistrationsByDay,
    IReadOnlyList<UserGroupCount> ByGender,
    IReadOnlyList<UserGroupCount> ByStatus,
    IReadOnlyList<UserRecentUser> RecentCustomers);

public sealed record UserDailyCount(DateOnly Date, int Count);

public sealed record UserGroupCount(string Label, int Count);

public sealed record UserRecentUser(
    Guid Id,
    string FullName,
    string Username,
    string? Phone,
    UserGender? Gender,
    DateTime CreatedAt,
    DateTime? LastLoginAt);
