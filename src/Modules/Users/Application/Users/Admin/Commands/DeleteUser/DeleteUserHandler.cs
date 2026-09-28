using System.Security.Claims;
using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Domain.Enums;

namespace Users.Application.Users.Admin.Commands.DeleteUser;

public sealed class DeleteUserHandler
{
    private readonly IUserRepository _userRepository;

    public DeleteUserHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task HandleAsync(
        Guid id,
        ClaimsPrincipal? principal = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.Role == UserRole.Administrator)
            await EnsureNotLastAdministratorAsync(
                user,
                principal,
                cancellationToken);

        await _userRepository.DeleteAsync(id, cancellationToken);
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
                "You cannot delete your own account.");
        }

        throw new ValidationException(
            "At least one administrator is required.");
    }
}
