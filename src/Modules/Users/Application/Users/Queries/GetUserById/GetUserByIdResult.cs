using Users.Domain.Enums;

namespace Users.Application.Users.Queries.GetUserById;

public sealed record GetUserByIdResult(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    UserGender? Gender,
    UserRole Role,
    UserStatus Status,
    DateTime CreatedAt);
