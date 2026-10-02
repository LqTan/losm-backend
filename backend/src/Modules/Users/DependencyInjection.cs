using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Users.Application.Abstractions;
using Users.Application.Users.Commands.LoginUser;
using Users.Application.Users.Commands.RegisterUser;
using Users.Application.Users.Commands.UpdateProfile;
using Users.Application.Users.Queries.GetUserById;
using Users.Infrastructure.Authentication;
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

        // Commands.
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<UpdateProfileHandler>();

        // Queries.
        services.AddScoped<GetUserByIdHandler>();
        
        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }
}