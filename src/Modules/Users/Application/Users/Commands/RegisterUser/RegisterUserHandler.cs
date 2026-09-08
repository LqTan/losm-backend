using Users.Application.Abstractions;
using Users.Application.Common.Exceptions;
using Users.Domain.Entities;

namespace Users.Application.Users.Commands.RegisterUser;

public class RegisterUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    public RegisterUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher
    )
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResult> HandleAsync(
        RegisterUserCommand command
    )
    {
        var existingUser = await _userRepository.GetByEmailAsync(command.Email);
        if (existingUser is not null)
            throw new ConflictException(
                "Email already exists."
            );
        var passwordHasher = _passwordHasher.Hash(command.Password);
        var user = new User(
            Guid.NewGuid(),
            command.Username,
            command.Email,
            passwordHasher
        );
        await _userRepository.AddAsync(user);
        return new RegisterUserResult(
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt
        );
    }
}