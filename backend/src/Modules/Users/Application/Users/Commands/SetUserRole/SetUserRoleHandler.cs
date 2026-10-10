using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Application.Users.Commands.SetUserRole;

public sealed class SetUserRoleHandler
{
    private readonly IUserRepository _repo;

    public SetUserRoleHandler(IUserRepository repo)
    {
        _repo = repo;
    }

    public async Task<SetUserRoleResult> HandleAsync(
        SetUserRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        var user = await _repo.GetByIdAsync(command.UserId);
        if (user is null)
            throw new NotFoundException($"User {command.UserId} not found.");

        if (!Enum.IsDefined(typeof(UserRole), command.Role))
            throw new ValidationException(
                $"Invalid role value: {command.Role}. Use 0 (User) or 1 (Admin).");

        user.SetRole((UserRole)command.Role);
        await _repo.UpdateAsync(user);

        return new SetUserRoleResult(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString());
    }
}