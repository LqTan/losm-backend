using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure.Seed;

public sealed class UsersSeeder
{
    private readonly UsersDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UsersSeeder> _logger;

    public UsersSeeder(
        UsersDbContext dbContext,
        IPasswordHasher passwordHasher,
        ILogger<UsersSeeder> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Seed Admin user: lequantan1974@gmail.com / Tan123456!
            var adminEmail = "lequantan1974@gmail.com";
            var existingAdmin = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == adminEmail, cancellationToken);
            if (existingAdmin is null)
            {
                var admin = new User(
                    id: Guid.NewGuid(),
                    username: "admin_tan",
                    email: adminEmail,
                    passwordHash: _passwordHasher.Hash("Tan123456!"),
                    role: UserRole.Admin
                );
                await _dbContext.Users.AddAsync(admin, cancellationToken);
                _logger.LogInformation("Seeded Admin user: {Email}", adminEmail);
            }

            // 2. Seed Standard User: user@losm.vn / Tan123456!
            var userEmail = "user@losm.vn";
            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == userEmail, cancellationToken);
            if (existingUser is null)
            {
                var normalUser = new User(
                    id: Guid.NewGuid(),
                    username: "tanle_user",
                    email: userEmail,
                    passwordHash: _passwordHasher.Hash("Tan123456!"),
                    role: UserRole.User
                );
                await _dbContext.Users.AddAsync(normalUser, cancellationToken);
                _logger.LogInformation("Seeded regular User: {Email}", userEmail);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed default users.");
        }
    }
}
