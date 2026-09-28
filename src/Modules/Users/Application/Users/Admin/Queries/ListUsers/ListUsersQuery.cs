using Common.Application.Models;
using Users.Domain.Enums;

namespace Users.Application.Users.Admin.Queries.ListUsers;

public sealed record ListUsersQuery(
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
    UserRole? ExcludeRole = null);

public sealed record ListUsersResult(
    IReadOnlyList<UserListItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public static ListUsersResult From(
        PagedResult<UserListItem> paged) =>
        new(
            paged.Items,
            paged.Page,
            paged.PageSize,
            paged.TotalCount,
            paged.TotalPages);
}

public sealed record UserListItem(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Gender,
    string Role,
    string Status,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime ChangedAt);
