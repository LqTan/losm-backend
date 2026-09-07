using Microsoft.AspNetCore.Identity;
using Users.Application.Abstractions;
using Users.Domain.Entities;

namespace Users.Infrastructure.Authentication;

public class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _passwordHasher = new();
    public string Hash(string password)
    {
        return _passwordHasher.HashPassword(null!, password);
    }
    public bool Verify(
        string password,
        string passwordHash
    )
    {
        var result = _passwordHasher.VerifyHashedPassword(
            null!,
            passwordHash,
            password
        );
        return result != PasswordVerificationResult.Failed;
    }
}