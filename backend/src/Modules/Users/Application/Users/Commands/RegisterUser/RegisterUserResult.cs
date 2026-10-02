namespace Users.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserResult(
    Guid Id,
    string Username,
    string Email,
    DateTime CreatedAt
);