using AgentCore.Application.Abstractions;
using AgentCore.Application.Agent.Commands.ExecuteAgent;
using AgentCore.Application.Agent.Queries.GetAgentSessionHistory;
using AgentCore.Infrastructure.AgentRuntime;
using AgentCore.Infrastructure.Llm;
using AgentCore.Infrastructure.Persistence;
using AgentCore.Infrastructure.Repositories;
using AgentCore.Infrastructure.Tools;
using AgentCore.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentCore;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentCore(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<AgentCoreDbContext>(
            options => options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
            )
        );

        services.Configure<AgentRunnerOptions>(
            configuration.GetSection("AgentCore")
        );

        services.AddScoped<IAgentSessionRepository, AgentSessionRepository>();
        services.AddScoped<IAgentTool, SearchPlacesTool>();
        services.AddScoped<IAgentTool, PlaceReviewsTool>();
        services.AddScoped<IAgentTool, GetCurrentUserTool>();
        services.AddScoped<IAgentToolRegistry, ArgentToolRegistry>();
        services.AddScoped<IAgentExecutionContext, AgentExecutionContext>();
        services.AddScoped<IAgentResponseValidator, GroundingValidator>();
        services.AddScoped<IAgentRunner, AgentRunner>();
        services.AddScoped<ExecuteAgentHandler>();
        services.AddScoped<GetAgentSessionHistoryHandler>();

        services.AddHttpClient<IAgentModelClient, MiniMaxAgentClient>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["Minimax:BaseUrl"]
                ?? "https://api.minimax.io/v1/"
            );
        });

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }
}
