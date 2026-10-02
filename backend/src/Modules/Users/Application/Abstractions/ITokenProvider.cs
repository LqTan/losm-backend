using Users.Domain.Entities;

namespace Users.Application.Abstractions;

public interface ITokenProvider
{
    string Create(User user);
}