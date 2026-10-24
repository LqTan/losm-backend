using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using AgentCore.Infrastructure.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentCore.Tests.Validation;

public class GroundingValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenNoSuccessfulEvidence_ReturnsOriginalAnswer()
    {
        var modelClient = new Mock<IAgentModelClient>();
        var validator = new GroundingValidator(modelClient.Object, null, NullLogger<GroundingValidator>.Instance);

        var toolExecutions = new List<AgentToolExecution>
        {
            new("tool_1", "{}", false)
        };

        var result = await validator.ValidateAsync("Candidate answer", toolExecutions);

        Assert.Equal("Candidate answer", result);
        modelClient.Verify(x => x.SendAsync(It.IsAny<IReadOnlyList<AgentModelMessage>>(), It.IsAny<IReadOnlyList<IAgentTool>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAsync_WhenModelThrowsTimeout_ReturnsOriginalAnswerGracefully()
    {
        var modelClient = new Mock<IAgentModelClient>();
        modelClient
            .Setup(x => x.SendAsync(It.IsAny<IReadOnlyList<AgentModelMessage>>(), It.IsAny<IReadOnlyList<IAgentTool>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request was canceled due to HttpClient.Timeout of 60 seconds elapsing."));

        var validator = new GroundingValidator(modelClient.Object, null, NullLogger<GroundingValidator>.Instance);

        var toolExecutions = new List<AgentToolExecution>
        {
            new("find_transit_journey", "{\"status\":\"no_direct_journey\"}", true)
        };

        var result = await validator.ValidateAsync("Candidate answer", toolExecutions);

        Assert.Equal("Candidate answer", result);
    }

    [Fact]
    public async Task ValidateAsync_WhenModelReturnsToolCalls_ReturnsOriginalAnswer()
    {
        var modelClient = new Mock<IAgentModelClient>();
        modelClient
            .Setup(x => x.SendAsync(It.IsAny<IReadOnlyList<AgentModelMessage>>(), It.IsAny<IReadOnlyList<IAgentTool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentModelResponse(
                new AgentModelMessage(AgentModelRole.Assistant, null),
                new[] { new AgentModelToolCall("call_1", "some_tool", "{}") }
            ));

        var validator = new GroundingValidator(modelClient.Object, null, NullLogger<GroundingValidator>.Instance);

        var toolExecutions = new List<AgentToolExecution>
        {
            new("find_transit_journey", "{\"status\":\"ok\"}", true)
        };

        var result = await validator.ValidateAsync("Candidate answer", toolExecutions);

        Assert.Equal("Candidate answer", result);
    }

    [Fact]
    public async Task ValidateAsync_WhenModelReturnsValidContent_ReturnsValidatedContent()
    {
        var modelClient = new Mock<IAgentModelClient>();
        modelClient
            .Setup(x => x.SendAsync(It.IsAny<IReadOnlyList<AgentModelMessage>>(), It.IsAny<IReadOnlyList<IAgentTool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentModelResponse(
                new AgentModelMessage(AgentModelRole.Assistant, "Grounding-verified answer"),
                Array.Empty<AgentModelToolCall>()
            ));

        var validator = new GroundingValidator(modelClient.Object, null, NullLogger<GroundingValidator>.Instance);

        var toolExecutions = new List<AgentToolExecution>
        {
            new("find_transit_journey", "{\"status\":\"ok\"}", true)
        };

        var result = await validator.ValidateAsync("Candidate answer", toolExecutions);

        Assert.Equal("Grounding-verified answer", result);
    }

    [Fact]
    public async Task ValidateAsync_WhenEvidenceIsMassive_TruncatesEvidencePayload()
    {
        IReadOnlyList<AgentModelMessage>? capturedMessages = null;

        var modelClient = new Mock<IAgentModelClient>();
        modelClient
            .Setup(x => x.SendAsync(It.IsAny<IReadOnlyList<AgentModelMessage>>(), It.IsAny<IReadOnlyList<IAgentTool>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<AgentModelMessage>, IReadOnlyCollection<IAgentTool>, CancellationToken>((msgs, _, _) => capturedMessages = msgs)
            .ReturnsAsync(new AgentModelResponse(
                new AgentModelMessage(AgentModelRole.Assistant, "Validated"),
                Array.Empty<AgentModelToolCall>()
            ));

        var validator = new GroundingValidator(modelClient.Object, null, NullLogger<GroundingValidator>.Instance);

        var massiveResult = new string('A', 5000);
        var toolExecutions = new List<AgentToolExecution>
        {
            new("find_transit_journey", massiveResult, true)
        };

        var result = await validator.ValidateAsync("Candidate answer", toolExecutions);

        Assert.Equal("Validated", result);
        Assert.NotNull(capturedMessages);
        var userMsg = capturedMessages!.First(m => m.Role == AgentModelRole.User);
        Assert.Contains("[truncated]", userMsg.Content);
        Assert.DoesNotContain(massiveResult, userMsg.Content);
    }
}
