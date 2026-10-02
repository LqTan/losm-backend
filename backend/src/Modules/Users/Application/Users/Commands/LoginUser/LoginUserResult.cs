namespace Users.Application.Users.Commands.LoginUser;

public sealed record LoginUserResult(
    Guid Id,
    string Username,
    string Email,
    string Token
);