using Common.Application.Exceptions;
using Users.Application.Abstractions;

namespace Users.Application.Users.Admin.Commands.VerifyCurrentPassword;

public sealed record VerifyCurrentPasswordCommand(
    Guid UserId,
    string Password);

/// <summary>
/// Re-checks the password of the signed-in admin before a sensitive
/// operation (resetting someone else's password). Returns a bool instead of
/// throwing so the UI can render the message inline in the form.
/// </summary>
public sealed class VerifyCurrentPasswordHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public VerifyCurrentPasswordHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> HandleAsync(
        VerifyCurrentPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Password))
            return false;

        var user = await _userRepository.GetByIdAsync(command.UserId);
        if (user is null)
            throw new KeyNotFoundException("User not found.");

        return _passwordHasher.Verify(command.Password, user.PasswordHash);
    }
}
