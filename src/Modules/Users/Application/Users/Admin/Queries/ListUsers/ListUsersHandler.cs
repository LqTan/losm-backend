using Common.Application.Models;
using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Application.Users.Admin.Queries.ListUsers;

public sealed class ListUsersHandler
{
    private readonly IUserRepository _userRepository;

    public ListUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PagedResult<UserListItem>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var criteria = new UserSearchCriteria
        {
            Keyword = query.Keyword,
            Role = query.Role,
            Status = query.Status,
            Gender = query.Gender,
            From = query.From,
            To = query.To,
            SortBy = query.SortBy,
            SortDescending = query.SortDescending,
            Page = query.Page,
            PageSize = query.PageSize,
            ExcludeRole = query.ExcludeRole,
        };

        var (users, totalCount) = await _userRepository.SearchAsync(
            criteria,
            cancellationToken);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        return new PagedResult<UserListItem>(
            users.Select(Map).ToList(),
            page,
            pageSize,
            totalCount);
    }

    private static UserListItem Map(User user) =>
        new(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Phone,
            user.Gender?.ToString(),
            user.Role.ToString(),
            user.Status.ToString(),
            user.LastLoginAt,
            user.CreatedAt,
            user.ChangedAt);
}
