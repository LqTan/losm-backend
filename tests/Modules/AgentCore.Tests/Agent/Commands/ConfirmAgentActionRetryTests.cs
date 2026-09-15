using System.Text.Json;
using AgentCore.Application.Agent.Commands.ConfirmAgentAction;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using AgentCore.Infrastructure.Dispatchers;
using AgentCore.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentCore.Tests.Agent.Commands;

public class ConfirmAgentActionRetryTests
{
    [Fact]
    public async Task ConfirmAsync_WhenActionIsPartiallyFailed_ReplaysWithoutExecuting()
    {
        var store = new InMemoryPendingActionStore();

        var original = new PendingAgentAction(
            id: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            actionType: "create_meeting",
            payloadJson: "{}",
            description: "Meeting",
            confirmationId: Guid.NewGuid());
        original.PartiallyFail("{}", "calendar ok, email failed");
        await store.UpdateAsync(original);

        var handler = new ConfirmAgentActionHandler(
            store,
            new AgentActionDispatcherResolver(
                Array.Empty<AgentCore.Application.Abstractions.IAgentActionDispatcher>()),
            NullLogger<ConfirmAgentActionHandler>.Instance);

        var result = await handler.HandleAsync(
            new ConfirmAgentActionCommand(original.Id, original.UserId),
            CancellationToken.None);

        Assert.Equal(
            "PartiallyFailed",
            result.Outcome.Status);
        Assert.Contains("partially failed", result.Outcome.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmAsync_WhenActionIsCompleted_Replays()
    {
        var store = new InMemoryPendingActionStore();

        var original = new PendingAgentAction(
            id: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            actionType: "create_meeting",
            payloadJson: "{}",
            description: "Meeting",
            confirmationId: Guid.NewGuid());
        original.Complete("{\"ok\":true}");
        await store.UpdateAsync(original);

        var handler = new ConfirmAgentActionHandler(
            store,
            new AgentActionDispatcherResolver(
                Array.Empty<AgentCore.Application.Abstractions.IAgentActionDispatcher>()),
            NullLogger<ConfirmAgentActionHandler>.Instance);

        var result = await handler.HandleAsync(
            new ConfirmAgentActionCommand(original.Id, original.UserId),
            CancellationToken.None);

        Assert.Equal("Completed", result.Outcome.Status);
    }
}
