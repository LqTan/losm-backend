namespace Users.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Username,
    string Email,
    string Password
);