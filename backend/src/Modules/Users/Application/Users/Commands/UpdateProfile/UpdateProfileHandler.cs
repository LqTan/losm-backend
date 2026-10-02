using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Application.Users.Commands.UpdateProfile;

public class UpdateProfileHandler
{
    private readonly IUserRepository _userRepository;

    public UpdateProfileHandler(
        IUserRepository userRepository
    )
    {
        _userRepository = userRepository;
    }

    public async Task<UpdateProfileResult?> HandleAsync(
        UpdateProfileCommand command
    )
    {
        var user = await _userRepository
            .GetByIdAsync(command.UserId);
        if (user is null)
            return null;
        user.UpdateProfile(command.Username);
        await _userRepository.UpdateAsync(user);
        return new UpdateProfileResult(
            user.Id,
            user.Username,
            user.Email
        );
    }
}