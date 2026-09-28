using System.Security.Claims;
using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Application.Users.Admin.Models;
using Users.Domain.Enums;

namespace Users.Application.Users.Admin.Queries.GetAdminProfile;

public sealed class GetAdminProfileHandler
{
    private readonly IUserRepository _userRepository;

    public GetAdminProfileHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AdminUserResult> HandleAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var idClaim = principal.FindFirstValue(
            ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idClaim, out var userId))
            throw new UnauthorizedException(
                "The current session is no longer valid.");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new UnauthorizedException(
                "The current session is no longer valid.");

        if (user.Role != UserRole.Administrator)
            throw new ForbiddenException(
                "This account is not allowed to use the admin panel.");

        return AdminUserResult.From(user);
    }
}
