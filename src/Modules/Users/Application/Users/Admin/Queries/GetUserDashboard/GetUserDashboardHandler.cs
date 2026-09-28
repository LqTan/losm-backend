using Users.Application.Abstractions;

namespace Users.Application.Users.Admin.Queries.GetUserDashboard;

public sealed record GetUserDashboardQuery(
    DateTime? From = null,
    DateTime? To = null);

public sealed record GetUserDashboardResult(
    DateTime From,
    DateTime To,
    int TotalCustomers,
    int ActiveCustomers,
    int BlockedCustomers,
    int NewCustomers,
    int LoggedInCustomers,
    int TotalAdministrators,
    IReadOnlyList<DashboardSeriesPoint> RegistrationsByDay,
    IReadOnlyList<DashboardGroup> ByGender,
    IReadOnlyList<DashboardGroup> ByStatus,
    IReadOnlyList<DashboardRecentUser> RecentCustomers);

public sealed record DashboardSeriesPoint(string Date, int Count);

public sealed record DashboardGroup(string Label, int Count);

public sealed record DashboardRecentUser(
    Guid Id,
    string FullName,
    string Username,
    string? Phone,
    string? Gender,
    string CreatedAt,
    string? LastLoginAt);

public sealed class GetUserDashboardHandler
{
    private const int MaxRangeInDays = 366;

    private readonly IUserRepository _userRepository;

    public GetUserDashboardHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserDashboardResult> HandleAsync(
        GetUserDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveRange(query.From, query.To);

        var stats = await _userRepository.GetDashboardStatsAsync(
            from,
            to,
            cancellationToken);

        return new GetUserDashboardResult(
            from,
            to,
            stats.TotalCustomers,
            stats.ActiveCustomers,
            stats.BlockedCustomers,
            stats.NewCustomers,
            stats.LoggedInCustomers,
            stats.TotalAdministrators,
            stats.RegistrationsByDay
                .Select(x => new DashboardSeriesPoint(
                    x.Date.ToString("yyyy-MM-dd"),
                    x.Count))
                .ToList(),
            stats.ByGender
                .Select(x => new DashboardGroup(x.Label, x.Count))
                .ToList(),
            stats.ByStatus
                .Select(x => new DashboardGroup(x.Label, x.Count))
                .ToList(),
            stats.RecentCustomers
                .Select(x => new DashboardRecentUser(
                    x.Id,
                    x.FullName,
                    x.Username,
                    x.Phone,
                    x.Gender?.ToString(),
                    x.CreatedAt.ToString("O"),
                    x.LastLoginAt?.ToString("O")))
                .ToList());
    }

    private static (DateTime From, DateTime To) ResolveRange(
        DateTime? from,
        DateTime? to)
    {
        var today = DateTime.UtcNow.Date;

        var resolvedTo = (to ?? today).Date;
        if (resolvedTo > today)
            resolvedTo = today;

        var resolvedFrom = (from ?? resolvedTo.AddDays(-29)).Date;
        if (resolvedFrom > resolvedTo)
            resolvedFrom = resolvedTo;

        if ((resolvedTo - resolvedFrom).TotalDays > MaxRangeInDays)
        {
            resolvedFrom = resolvedTo.AddDays(-MaxRangeInDays);
        }

        return (
            resolvedFrom,
            resolvedTo.AddDays(1).AddTicks(-1));
    }
}
