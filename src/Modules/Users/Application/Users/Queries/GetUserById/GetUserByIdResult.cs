namespace Users.Application.Users.Queries.GetUserById;

public sealed record GetUserByIdResult(
    Guid Id,
    string Username,
    string Email,
    DateTime CreatedAt
);