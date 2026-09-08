using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using AgentCore.Infrastructure.Services;

namespace AgentCore.Infrastructure.Planning;

public sealed class LlmAgentPlanner : IAgentPlanner
{
    private readonly IAgentModelClient _modelClient;
    private readonly IAgentToolRegistry _toolRegistry;
    public LlmAgentPlanner(
        IAgentModelClient modelClient,
        IAgentToolRegistry toolRegistry
    )
    {
        _modelClient = modelClient;
        _toolRegistry = toolRegistry;
    }

    public async Task<string> CreateAsync(
        string input,
        double latitude,
        double longitude,
        IReadOnlyCollection<AgentMessage> conversation,
        CancellationToken cancellationToken = default
    )
    {
        var tools = _toolRegistry.GetAll().Select(x => new
        {
            x.Name,
            x.Description
        });
        var history = conversation.OrderBy(x => x.CreatedAt).Select(x => new
        {
            role = x.Role.ToString(),
            x.Content
        });

        var payload = JsonSerializer.Serialize(
            new
            {
                request = input,
                location = new
                {
                    latitude,
                    longitude
                },
                conversation = history,
                availableTools = tools
            }
        );

        var messages = new List<AgentModelMessage>
        {
            new(
                AgentModelRole.System,
                PromptFile.Load("planner-system.txt")
            ),
            new(
                AgentModelRole.User,
                payload
            )
        };

        var response = await _modelClient.SendAsync(
            messages,
            Array.Empty<IAgentTool>(),
            cancellationToken
        );
        if (response.HasToolCalls)
        {
            throw new InvalidOperationException(
                "Planner cannot execute tools."
            );
        }

        var plan = response.AssistantMessage.Content;
        if (string.IsNullOrWhiteSpace(plan))
        {
            throw new InvalidOperationException(
                "Planner returned an empty plan."
            );
        }

        return plan;
    }
}
