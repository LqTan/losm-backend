namespace Users.Application.Users.Commands.UpdateProfile;

public sealed record UpdateProfileResult(
    Guid Id,
    string Username,
    string Email
);