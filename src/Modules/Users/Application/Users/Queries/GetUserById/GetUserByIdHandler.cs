using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Application.Users.Queries.GetUserById;

public class GetUserByIdHandler
{
    private readonly IUserRepository _userRepository;
    public GetUserByIdHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    public async Task<GetUserByIdResult?> HandleAsync(GetUserByIdQuery query)
    {
        var user = await _userRepository.GetByIdAsync(query.Id);
        if (user is null)
            return null;
        return new GetUserByIdResult(
            user.Id,
            user.Username,
            user.Email,
            user.CreatedAt
        );
    }
}