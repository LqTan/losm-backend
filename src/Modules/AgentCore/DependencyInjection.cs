using AgentCore.Application.Abstractions;
using AgentCore.Application.Agent.Commands.ApproveAgentPlan;
using AgentCore.Application.Agent.Commands.ConfirmAgentAction;
using AgentCore.Application.Agent.Commands.CreateAgentPlan;
using AgentCore.Application.Agent.Commands.ExecuteAgent;
using AgentCore.Application.Agent.Queries.GetAgentSessionHistory;
using AgentCore.Infrastructure.AgentRuntime;
using AgentCore.Infrastructure.Llm;
using AgentCore.Infrastructure.Persistence;
using AgentCore.Infrastructure.Planning;
using AgentCore.Infrastructure.Repositories;
using AgentCore.Infrastructure.Stores;
using AgentCore.Infrastructure.Tools;
using AgentCore.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Places.Application.SavedPlaces.Commands.SavePlace;
using Reviews.Application.Reviews.Commands.CreateReview;

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
        services.AddScoped<IAgentPlanner, LlmAgentPlanner>();
        services.AddScoped<IAgentPlanRepository, AgentPlanRepository>();
        services.AddScoped<CreateAgentPlanHandler>();
        services.AddScoped<ApproveAgentPlanHandler>();
        services.AddScoped<ConfirmAgentActionHandler>();
        services.AddScoped<IAgentTool, SearchPlacesTool>();
        services.AddScoped<IAgentTool, PlaceReviewsTool>();
        services.AddScoped<IAgentTool, GetCurrentUserTool>();
        services.AddScoped<IAgentTool, SavePlaceTool>();
        services.AddScoped<IAgentTool, CreateReviewTool>();
        services.AddScoped<IAgentToolRegistry, ArgentToolRegistry>();
        services.AddScoped<IAgentExecutionContext, AgentExecutionContext>();
        services.AddSingleton<IPendingActionStore, InMemoryPendingActionStore>();
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
