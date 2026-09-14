using AgentCore.Application.Abstractions;
using AgentCore.Application.Agent.Commands.ApproveAgentPlan;
using AgentCore.Application.Agent.Commands.ConfirmAgentAction;
using AgentCore.Application.Agent.Commands.CreateAgentPlan;
using AgentCore.Application.Agent.Commands.ExecuteAgent;
using AgentCore.Application.Agent.Queries.GetAgentSessionHistory;
using AgentCore.Infrastructure.AgentRuntime;
using AgentCore.Infrastructure.Dispatchers;
using AgentCore.Infrastructure.Llm;
using AgentCore.Infrastructure.Meeting;
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
        services.AddScoped<ILastSearchContextStore, LastSearchContextStore>();
        services.AddScoped<IPendingActionStore, EfPendingActionStore>();

        services.AddScoped<CreateAgentPlanHandler>();
        services.AddScoped<ApproveAgentPlanHandler>();
        services.AddScoped<ConfirmAgentActionHandler>();
        services.AddScoped<ExecuteAgentHandler>();
        services.AddScoped<GetAgentSessionHistoryHandler>();

        services.AddScoped<IAgentTool, SearchPlacesTool>();
        services.AddScoped<IAgentTool, PlaceReviewsTool>();
        services.AddScoped<IAgentTool, GetCurrentUserTool>();
        services.AddScoped<IAgentTool, SavePlaceTool>();
        services.AddScoped<IAgentTool, CreateReviewTool>();
        services.AddScoped<IAgentTool, CreateMeetingTool>();

        services.AddScoped<IAgentToolRegistry, AgentToolRegistry>();

        services.AddScoped<IAgentExecutionContext, AgentExecutionContext>();
        services.AddScoped<IAgentResponseValidator, GroundingValidator>();
        services.AddScoped<IAgentRunner, AgentRunner>();

        services.AddScoped<IAgentActionDispatcher, SavePlaceDispatcher>();
        services.AddScoped<IAgentActionDispatcher, CreateReviewDispatcher>();
        services.AddScoped<IAgentActionDispatcher, CreateMeetingDispatcher>();
        services.AddScoped<IAgentActionDispatcherResolver, AgentActionDispatcherResolver>();

        var n8nBaseUrl = configuration["N8n:BaseUrl"];
        if (string.IsNullOrWhiteSpace(n8nBaseUrl))
        {
            services.AddSingleton<IMeetingAutomationClient, FakeN8nMeetingClient>();
        }
        else
        {
            services.AddHttpClient<IMeetingAutomationClient, N8nMeetingClient>(client =>
            {
                client.BaseAddress = new Uri(n8nBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            });
        }

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
