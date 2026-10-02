using AgentCore.Application.Agent.Commands.ConfirmAgentAction;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using AgentCore.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentCore.Tests.Agent.Commands;

public class ConfirmAgentActionEmailValidationTests
{
    [Fact]
    public async Task ConfirmAsync_WhenCreateMeetingHasInvalidEmail_Throws()
    {
        var store = new InMemoryPendingActionStore();
        var action = CreatePending(
            "create_meeting",
            payload: """
            {
              "title":"Meet",
              "attendees":["valid@example.com","not-an-email"]
            }
            """);

        await store.AddAsync(action);

        var handler = new ConfirmAgentActionHandler(
            store,
            new AgentCore.Infrastructure.Dispatchers.AgentActionDispatcherResolver(
                Array.Empty<AgentCore.Application.Abstractions.IAgentActionDispatcher>()),
            NullLogger<ConfirmAgentActionHandler>.Instance);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new ConfirmAgentActionCommand(action.Id, action.UserId),
                CancellationToken.None));

        Assert.Contains("not-an-email", ex.Message);
    }

    [Fact]
    public async Task ConfirmAsync_WhenCreateMeetingHasAllValidEmails_Succeeds()
    {
        var store = new InMemoryPendingActionStore();
        var action = CreatePending(
            "create_meeting",
            payload: """
            {
              "title":"Meet",
              "attendees":["alice@example.com","bob@example.com"]
            }
            """);

        await store.AddAsync(action);

        var handler = new ConfirmAgentActionHandler(
            store,
            new AgentCore.Infrastructure.Dispatchers.AgentActionDispatcherResolver(
                Array.Empty<AgentCore.Application.Abstractions.IAgentActionDispatcher>()),
            NullLogger<ConfirmAgentActionHandler>.Instance);

        var result = await handler.HandleAsync(
            new ConfirmAgentActionCommand(action.Id, action.UserId),
            CancellationToken.None);

        Assert.NotNull(result.Outcome);
        Assert.Equal(action.Id, result.Outcome.ActionId);
    }

    private static PendingAgentAction CreatePending(string actionType, string payload)
    {
        var action = new PendingAgentAction(
            id: Guid.NewGuid(),
            sessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            actionType: actionType,
            payloadJson: payload,
            description: "test",
            confirmationId: null,
            idempotencyKey: null);

        return action;
    }
}
