using Users.Application.Abstractions;

namespace Users.Application.Users.Commands.LoginUser;

public class LoginUserHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenProvider _tokenProvider;

    public LoginUserHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider
    )
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
    }
    public async Task<LoginUserResult> HandleAsync(
        LoginUserCommand command
    )
    {
        var user = await _userRepository.GetByEmailAsync(command.Email);
        if (user is null)
            throw new InvalidOperationException(
                "Invalid email or password."
            );
        var validPassword = _passwordHasher.Verify(
            command.Password,
            user.PasswordHash
        );
        if (!validPassword)
            throw new InvalidOperationException(
                "Invalid email or password."
            );
        var token = _tokenProvider.Create(user);
        return new LoginUserResult(
            user.Id,
            user.Username,
            user.Email,
            token
        );
    }
}