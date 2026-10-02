using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sandbox.Application.Abstractions;
using Sandbox.Application.Todos.Commands;
using Sandbox.Application.Todos.Queries;
using Sandbox.Infrastructure.Persistence;
using Sandbox.Infrastructure.Repositories;

namespace Sandbox;

public static class DependencyInjection
{
    public static IServiceCollection AddSandbox(
        this IServiceCollection services,
        string connectionString
    )
    {
        services.AddDbContext<SandboxDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<ITodoRepository, TodoRepository>();
        services.AddScoped<CreateTodo>();
        services.AddScoped<GetTodos>();
        services.AddScoped<GetTodoById>();

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        
        return services;
    }
}