namespace Users.Application.Users.Commands.UpdateProfile;

public sealed record UpdateProfileCommand(
    Guid UserId,
    string Username
);