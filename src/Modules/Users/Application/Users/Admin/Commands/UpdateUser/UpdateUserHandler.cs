using System.Security.Claims;
using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Application.Users.Admin.Models;
using Users.Application.Users.Validation;
using Users.Domain.Enums;

namespace Users.Application.Users.Admin.Commands.UpdateUser;

public sealed record UpdateUserCommand(
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Gender,
    string Role,
    string Status,
    bool Notify);

public sealed class UpdateUserHandler
{
    private readonly IUserRepository _userRepository;

    public UpdateUserHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AdminUserResult> HandleAsync(
        Guid id,
        UpdateUserCommand command,
        ClaimsPrincipal? principal = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        var username = UserInputRules.RequireText(
            command.Username, "Username", 100);
        var email = UserInputRules.RequireEmail(command.Email);
        var fullName = UserInputRules.RequireText(
            command.FullName, "Full name", 255);
        var phone = UserInputRules.NormalizePhone(command.Phone);
        var gender = UserInputRules.ParseGender(command.Gender);
        var role = UserInputRules.ParseRole(command.Role);
        var status = UserInputRules.ParseStatus(command.Status);

        if (await _userRepository.ExistsByEmailAsync(
            email, id, cancellationToken))
        {
            throw new ConflictException("Email already exists.");
        }

        if (await _userRepository.ExistsByUsernameAsync(
            username, id, cancellationToken))
        {
            throw new ConflictException("Username already exists.");
        }

        if (user.Role == UserRole.Administrator
            && (role != UserRole.Administrator
                || status != UserStatus.Active))
        {
            await EnsureNotLastAdministratorAsync(
                user,
                principal,
                cancellationToken);
        }

        user.UpdateProfile(username);
        user.UpdateContact(email, phone, gender);
        user.UpdateDetails(
            fullName,
            phone,
            gender,
            role,
            status,
            command.Notify);

        await _userRepository.UpdateAsync(user);

        return AdminUserResult.From(user);
    }

    private async Task EnsureNotLastAdministratorAsync(
        Domain.Entities.User current,
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken)
    {
        var administrators = await _userRepository.SearchAsync(
            new UserSearchCriteria(
                Role: UserRole.Administrator,
                Page: 1,
                PageSize: 2),
            cancellationToken);

        if (administrators.TotalCount > 1)
            return;

        var selfIdClaim = principal?.FindFirstValue(
            ClaimTypes.NameIdentifier);
        if (Guid.TryParse(selfIdClaim, out var selfId)
            && selfId == current.Id)
        {
            throw new ValidationException(
                "You cannot demote or block your own account.");
        }

        throw new ValidationException(
            "At least one active administrator is required.");
    }
}
