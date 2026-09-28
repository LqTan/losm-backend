using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Users.Application.Abstractions;
using Users.Application.Users.Admin.Commands.AdminLogin;
using Users.Application.Users.Admin.Commands.CreateUser;
using Users.Application.Users.Admin.Commands.DeleteUser;
using Users.Application.Users.Admin.Commands.ResetUserPassword;
using Users.Application.Users.Admin.Commands.UpdateUser;
using Users.Application.Users.Admin.Commands.VerifyCurrentPassword;
using Users.Application.Users.Admin.Queries.GetAdminProfile;
using Users.Application.Users.Admin.Queries.GetAdminUserById;
using Users.Application.Users.Admin.Queries.GetUserDashboard;
using Users.Application.Users.Admin.Queries.ListUsers;
using Users.Application.Users.Commands.LoginUser;
using Users.Application.Users.Commands.RegisterUser;
using Users.Application.Users.Commands.UpdateProfile;
using Users.Application.Users.Queries.GetUserById;
using Users.Domain.Enums;
using Users.Infrastructure.Authentication;
using Users.Infrastructure.Notifications;
using Users.Infrastructure.Persistence;
using Users.Infrastructure.Repositories;

namespace Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUsers(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<UsersDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
            ));
        // Repository.
        services.AddScoped<IUserRepository, UserRepository>();

        // Authentication.
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenProvider, JwtTokenProvider>();

        // Notifications.
        services
            .AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection("Smtp"));
        services.AddScoped<IAccountNotifier, SmtpAccountNotifier>();

        // Commands.
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<UpdateProfileHandler>();
        services.AddScoped<AdminLoginHandler>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<DeleteUserHandler>();
        services.AddScoped<ResetUserPasswordHandler>();
        services.AddScoped<VerifyCurrentPasswordHandler>();

        // Queries.
        services.AddScoped<GetUserByIdHandler>();
        services.AddScoped<GetAdminProfileHandler>();
        services.AddScoped<ListUsersHandler>();
        services.AddScoped<GetAdminUserByIdHandler>();
        services.AddScoped<GetUserDashboardHandler>();

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }

    /// <summary>
    /// Creates the first administrator account when the system has none yet.
    /// </summary>
    public static async Task SeedAdministratorAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var enabled = configuration.GetValue("Admin:Seed:Enabled", true);
        if (!enabled)
            return;

        var email = configuration["Admin:Seed:Email"];
        var password = configuration["Admin:Seed:Password"];
        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var repository = services
            .GetRequiredService<IUserRepository>();
        if (await repository.ExistsAdministratorAsync(cancellationToken))
            return;

        var username = configuration["Admin:Seed:Username"];
        if (string.IsNullOrWhiteSpace(username))
            username = email.Split('@')[0];

        var fullName = configuration["Admin:Seed:FullName"];
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = "Administrator";

        var hasher = services.GetRequiredService<IPasswordHasher>();

        var admin = new Domain.Entities.User(
            Guid.NewGuid(),
            username,
            email,
            hasher.Hash(password),
            fullName,
            null,
            null,
            UserRole.Administrator,
            UserStatus.Active,
            false);

        await repository.AddAsync(admin);
    }
}
