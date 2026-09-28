namespace Users.Application.Users.Admin.Models;

public sealed record AdminUserResult(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Gender,
    string Role,
    string Status,
    bool NotifyOnAccountCreation,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime ChangedAt)
{
    public static AdminUserResult From(Domain.Entities.User user) =>
        new(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Phone,
            user.Gender?.ToString(),
            user.Role.ToString(),
            user.Status.ToString(),
            user.NotifyOnAccountCreation,
            user.LastLoginAt,
            user.CreatedAt,
            user.ChangedAt);
}
