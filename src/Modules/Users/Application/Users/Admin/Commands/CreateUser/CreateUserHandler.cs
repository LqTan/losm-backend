using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Application.Users.Admin.Models;
using Users.Application.Users.Validation;
using Users.Domain.Entities;

namespace Users.Application.Users.Admin.Commands.CreateUser;

public sealed record CreateUserCommand(
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Gender,
    string Role,
    string Status,
    string Password,
    bool Notify);

public sealed class CreateUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountNotifier _accountNotifier;

    public CreateUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAccountNotifier accountNotifier)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _accountNotifier = accountNotifier;
    }

    public async Task<AdminUserResult> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var username = UserInputRules.RequireText(
            command.Username, "Username", 100);
        var email = UserInputRules.RequireEmail(command.Email);
        var fullName = UserInputRules.RequireFullName(command.FullName);
        var phone = UserInputRules.NormalizePhone(command.Phone);
        var gender = UserInputRules.ParseGender(command.Gender);
        var role = UserInputRules.ParseRole(command.Role);
        var status = UserInputRules.ParseStatus(command.Status);

        UserInputRules.ValidatePassword(command.Password);

        if (await _userRepository.ExistsByEmailAsync(
            email, null, cancellationToken))
        {
            throw new ConflictException("Email already exists.");
        }

        if (await _userRepository.ExistsByUsernameAsync(
            username, null, cancellationToken))
        {
            throw new ConflictException("Username already exists.");
        }

        if (phone is not null
            && await _userRepository.ExistsByPhoneAsync(
                phone, null, cancellationToken))
        {
            throw new ConflictException("Phone number already exists.");
        }

        var user = new User(
            Guid.NewGuid(),
            username,
            email,
            _passwordHasher.Hash(command.Password),
            fullName,
            phone,
            gender,
            role,
            status,
            command.Notify);

        await _userRepository.AddAsync(user);

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

        return AdminUserResult.From(user);
    }
}
