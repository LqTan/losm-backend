namespace Users.Application.Users.Commands.SetUserRole;

public sealed record SetUserRoleCommand(Guid UserId, int Role);

public sealed record SetUserRoleResult(
    Guid Id,
    string Username,
    string Email,
    string Role);