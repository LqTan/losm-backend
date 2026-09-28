using Common.Application.Exceptions;
using Users.Application.Abstractions;
using Users.Application.Users.Admin.Models;

namespace Users.Application.Users.Admin.Queries.GetAdminUserById;

public sealed record GetAdminUserByIdQuery(Guid Id);

public sealed class GetAdminUserByIdHandler
{
    private readonly IUserRepository _userRepository;

    public GetAdminUserByIdHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AdminUserResult> HandleAsync(
        GetAdminUserByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(query.Id)
            ?? throw new KeyNotFoundException("User not found.");

        return AdminUserResult.From(user);
    }
}
