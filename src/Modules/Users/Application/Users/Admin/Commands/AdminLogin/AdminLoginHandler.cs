using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Domain.Enums;

namespace Users.Application.Users.Admin.Commands.AdminLogin;

public sealed record AdminLoginCommand(
    string Email,
    string Password);

public sealed record AdminLoginResult(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    string Token,
    DateTime? LastLoginAt,
    DateTime ExpiresAt);

public sealed class AdminLoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenProvider _tokenProvider;

    public AdminLoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenProvider = tokenProvider;
    }

    public async Task<AdminLoginResult> HandleAsync(
        AdminLoginCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Email)
            || string.IsNullOrWhiteSpace(command.Password))
        {
            throw new ValidationException(
                "Email and password are required.");
        }

        var user = await _userRepository.GetByEmailAsync(
            command.Email.Trim());

        if (user is null
            || !_passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new UnauthorizedException(
                "Invalid email or password.");
        }

        if (user.Role != UserRole.Administrator)
            throw new ForbiddenException(
                "This account is not allowed to sign in to the admin panel.");

        if (user.Status == UserStatus.Blocked)
            throw new ForbiddenException(
                "This account has been blocked.");

        user.RecordLogin();
        await _userRepository.UpdateAsync(user);

        var token = _tokenProvider.Create(user);

        return new AdminLoginResult(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            token,
            user.LastLoginAt,
            DateTime.UtcNow.AddHours(24));
    }
}
