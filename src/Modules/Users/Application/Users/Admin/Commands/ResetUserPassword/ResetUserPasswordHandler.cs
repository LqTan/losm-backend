using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Application.Users.Validation;
using Users.Domain.Entities;

namespace Users.Application.Users.Admin.Commands.ResetUserPassword;

public sealed record ResetUserPasswordCommand(
    string Password,
    bool Notify);

public sealed class ResetUserPasswordHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountNotifier _accountNotifier;

    public ResetUserPasswordHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAccountNotifier accountNotifier)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accountNotifier = accountNotifier;
    }

    public async Task HandleAsync(
        Guid id,
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        UserInputRules.ValidatePassword(command.Password);

        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        user.ChangePassword(_passwordHasher.Hash(command.Password));
        await _userRepository.UpdateAsync(user);

        if (command.Notify)
        {
            await _accountNotifier.SendNewAccountAsync(
                new NewAccountNotification(
                    user.Email,
                    user.FullName,
                    user.Username,
                    command.Password),
                cancellationToken);
        }
    }
}
