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
using Microsoft.Extensions.Hosting;
using Places.Application.SavedPlaces.Commands.SavePlace;
using Reviews.Application.Reviews.Commands.CreateReview;

namespace AgentCore;

public static class DependencyInjection
{
    public static IServiceCollection AddAgentCore(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.AddDbContext<AgentCoreDbContext>(
            options => options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
            )
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
        services.AddScoped<IAgentTool, SearchMeetingPlacesTool>();
        services.AddScoped<IAgentTool, GeocodePlaceTool>();
        services.AddScoped<IAgentTool, PlaceReviewsTool>();
        services.AddScoped<IAgentTool, GetCurrentUserTool>();
        services.AddScoped<IAgentTool, SavePlaceTool>();
        services.AddScoped<IAgentTool, CreateReviewTool>();
        services.AddScoped<IAgentTool, CreateMeetingTool>();
        services.AddScoped<IAgentTool, RetryMeetingEmailsTool>();

        services.AddScoped<IAgentToolRegistry, AgentToolRegistry>();

        services.AddScoped<IAgentExecutionContext, AgentExecutionContext>();
        services.AddScoped<IAgentResponseValidator, GroundingValidator>();
        services.AddScoped<IAgentRunner, AgentRunner>();

        services.AddScoped<IAgentActionDispatcher, SavePlaceDispatcher>();
        services.AddScoped<IAgentActionDispatcher, CreateReviewDispatcher>();
        services.AddScoped<IAgentActionDispatcher, CreateMeetingDispatcher>();
        services.AddScoped<IAgentActionDispatcher, RetryMeetingEmailsDispatcher>();
        services.AddScoped<IAgentActionDispatcherResolver, AgentActionDispatcherResolver>();

        var n8nBaseUrl = configuration["N8n:BaseUrl"];
        var isProduction = environment.IsProduction();

        services
            .AddOptions<N8nMeetingClientOptions>()
            .Bind(configuration.GetSection("N8n"));

        if (string.IsNullOrWhiteSpace(n8nBaseUrl))
        {
            if (isProduction)
            {
                throw new InvalidOperationException(
                    "N8n:BaseUrl is not configured. Production requires a real n8n endpoint; FakeN8nMeetingClient is not allowed.");
            }
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

        services
            .AddOptions<MiniMaxAgentClientOptions>()
            .Bind(configuration.GetSection("Minimax"));

        services.AddHttpClient<IAgentModelClient, MiniMaxAgentClient>((sp, client) =>
        {
            var opts = sp
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<MiniMaxAgentClientOptions>>()
                .Value;

            if (string.IsNullOrWhiteSpace(opts.BaseUrl))
            {
                throw new InvalidOperationException(
                    "Minimax:BaseUrl is not configured.");
            }

            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds));
        });

        services.AddControllers()
            .AddApplicationPart(typeof(DependencyInjection).Assembly);
        return services;
    }
}
