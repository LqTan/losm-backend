using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Domain.Enums;

namespace Users.Application.Users.Commands.LoginUser;

public class LoginUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenProvider _tokenProvider;

    public LoginUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
    }
    public async Task<LoginUserResult> HandleAsync(
        LoginUserCommand command)
    {
        var user = await _userRepository.GetByEmailAsync(command.Email);
        if (user is null)
        {
            throw new UnauthorizedException(
                "Invalid email or password.");
        }
        var validPassword = _passwordHasher.Verify(
            command.Password,
            user.PasswordHash
        );
        if (!validPassword)
        {
            throw new UnauthorizedException(
                "Invalid email or password.");
        }
        if (user.Status == UserStatus.Blocked)
        {
            throw new ForbiddenException(
                "This account has been blocked.");
        }
        user.RecordLogin();
        await _userRepository.UpdateAsync(user);
        var token = _tokenProvider.Create(user);
        return new LoginUserResult(
            user.Id,
            user.Username,
            user.Email,
            token
        );
    }
}
