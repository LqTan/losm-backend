using System.Text.Json.Nodes;

using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using AgentCore.Infrastructure.AgentRuntime;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

namespace AgentCore.Tests.AgentRuntime;

public class AgentRunnerTests
{
    [Fact]
    public async Task RunAsync_WhenModelReturnsFinalAnswer_ReturnsValidatedAnswer()
    {
        var modelClient = new Mock<IAgentModelClient>();
        var toolRegistry = new Mock<IAgentToolRegistry>();
        var sessionRepository = new Mock<IAgentSessionRepository>();
        var executionContext = new Mock<IAgentExecutionContext>();
        var responseValidator = new Mock<IAgentResponseValidator>();

        AgentSession? createdSession = null;

        sessionRepository
            .Setup(x => x.AddAsync(
                It.IsAny<AgentSession>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<AgentSession, CancellationToken>(
                (session, _) => createdSession = session
            )
            .Returns(Task.CompletedTask);

        sessionRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<AgentSession>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        toolRegistry
            .Setup(x => x.GetAll())
            .Returns(Array.Empty<IAgentTool>());

        modelClient
            .Setup(x => x.SendAsync(
                It.IsAny<IReadOnlyList<AgentModelMessage>>(),
                It.IsAny<IReadOnlyCollection<IAgentTool>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(
                new AgentModelResponse(
                    new AgentModelMessage(
                        AgentModelRole.Assistant,
                        "Original answer"
                    ),
                    Array.Empty<AgentModelToolCall>()
                )
            );

        responseValidator
            .Setup(x => x.ValidateAsync(
                "Original answer",
                It.IsAny<IReadOnlyList<AgentToolExecution>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync("Validated answer");

        var runner = CreateRunner(
            modelClient.Object,
            toolRegistry.Object,
            sessionRepository.Object,
            executionContext.Object,
            responseValidator.Object
        );

        var userId = Guid.NewGuid();

        var result = await runner.RunAsync(
            "Hello",
            10.7769,
            106.7009,
            null,
            userId
        );

        Assert.Equal(
            "Validated answer",
            result.Answer
        );

        Assert.NotNull(createdSession);

        Assert.Equal(
            userId,
            createdSession!.UserId
        );

        Assert.Equal(
            2,
            createdSession.Messages.Count
        );

        Assert.Contains(
            createdSession.Messages,
            x =>
                x.Role == AgentMessageRole.User &&
                x.Content == "Hello"
        );

        Assert.Contains(
            createdSession.Messages,
            x =>
                x.Role == AgentMessageRole.Assistant &&
                x.Content == "Validated answer"
        );

        executionContext.Verify(
            x => x.SetUser(userId),
            Times.Once
        );

        executionContext.Verify(
            x => x.SetLocation(
                10.7769,
                106.7009
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task RunAsync_WhenModelCallsTool_ExecutesToolAndContinuesLoop()
    {
        var modelClient = new Mock<IAgentModelClient>();
        var toolRegistry = new Mock<IAgentToolRegistry>();
        var sessionRepository = new Mock<IAgentSessionRepository>();
        var executionContext = new Mock<IAgentExecutionContext>();
        var responseValidator = new Mock<IAgentResponseValidator>();
        var tool = new Mock<IAgentTool>();

        AgentSession? createdSession = null;

        sessionRepository
            .Setup(x => x.AddAsync(
                It.IsAny<AgentSession>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<AgentSession, CancellationToken>(
                (session, _) => createdSession = session
            )
            .Returns(Task.CompletedTask);

        sessionRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<AgentSession>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        tool
            .SetupGet(x => x.Name)
            .Returns("test_tool");

        tool
            .SetupGet(x => x.Description)
            .Returns("Test tool");

        tool
            .SetupGet(x => x.ParametersSchema)
            .Returns(
                JsonNode.Parse(
                    """
                    {
                      "type": "object",
                      "properties": {}
                    }
                    """
                )!
            );

        tool
            .Setup(x => x.ExecuteAsync(
                It.IsAny<System.Text.Json.JsonElement>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(
                """{"result":"success"}"""
            );

        toolRegistry
            .Setup(x => x.GetAll())
            .Returns(
                new[]
                {
                    tool.Object
                }
            );

        toolRegistry
            .Setup(x => x.GetRequired("test_tool"))
            .Returns(tool.Object);

        var toolCall = new AgentModelToolCall(
            "call_1",
            "test_tool",
            "{}"
        );

        var responses = new Queue<AgentModelResponse>();

        responses.Enqueue(
            new AgentModelResponse(
                new AgentModelMessage(
                    AgentModelRole.Assistant,
                    null,
                    new[]
                    {
                        toolCall
                    }
                ),
                new[]
                {
                    toolCall
                }
            )
        );

        responses.Enqueue(
            new AgentModelResponse(
                new AgentModelMessage(
                    AgentModelRole.Assistant,
                    "Final answer"
                ),
                Array.Empty<AgentModelToolCall>()
            )
        );

        modelClient
            .Setup(x => x.SendAsync(
                It.IsAny<IReadOnlyList<AgentModelMessage>>(),
                It.IsAny<IReadOnlyCollection<IAgentTool>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(() => responses.Dequeue());

        responseValidator
            .Setup(x => x.ValidateAsync(
                "Final answer",
                It.IsAny<IReadOnlyList<AgentToolExecution>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync("Final answer");

        var runner = CreateRunner(
            modelClient.Object,
            toolRegistry.Object,
            sessionRepository.Object,
            executionContext.Object,
            responseValidator.Object
        );

        var result = await runner.RunAsync(
            "Use a tool",
            10.7769,
            106.7009,
            null,
            Guid.NewGuid()
        );

        Assert.Equal(
            "Final answer",
            result.Answer
        );

        Assert.NotNull(createdSession);

        Assert.Single(
            createdSession!.ToolCalls
        );

        var persistedToolCall =
            createdSession.ToolCalls.Single();

        Assert.Equal(
            "test_tool",
            persistedToolCall.ToolName
        );

        Assert.Equal(
            AgentToolCallStatus.Completed,
            persistedToolCall.Status
        );

        Assert.Equal(
            """{"result":"success"}""",
            persistedToolCall.ResultJson
        );

        tool.Verify(
            x => x.ExecuteAsync(
                It.IsAny<System.Text.Json.JsonElement>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );

        modelClient.Verify(
            x => x.SendAsync(
                It.IsAny<IReadOnlyList<AgentModelMessage>>(),
                It.IsAny<IReadOnlyCollection<IAgentTool>>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task RunAsync_WhenToolFails_ReturnsErrorToModelAndContinues()
    {
        var modelClient = new Mock<IAgentModelClient>();
        var toolRegistry = new Mock<IAgentToolRegistry>();
        var sessionRepository = new Mock<IAgentSessionRepository>();
        var executionContext = new Mock<IAgentExecutionContext>();
        var responseValidator = new Mock<IAgentResponseValidator>();
        var tool = new Mock<IAgentTool>();

        AgentSession? createdSession = null;

        sessionRepository
            .Setup(x => x.AddAsync(
                It.IsAny<AgentSession>(),
                It.IsAny<CancellationToken>()
            ))
            .Callback<AgentSession, CancellationToken>(
                (session, _) => createdSession = session
            )
            .Returns(Task.CompletedTask);

        sessionRepository
            .Setup(x => x.UpdateAsync(
                It.IsAny<AgentSession>(),
                It.IsAny<CancellationToken>()
            ))
            .Returns(Task.CompletedTask);

        tool
            .SetupGet(x => x.Name)
            .Returns("failing_tool");

        tool
            .Setup(x => x.ExecuteAsync(
                It.IsAny<System.Text.Json.JsonElement>(),
                It.IsAny<CancellationToken>()
            ))
            .ThrowsAsync(
                new InvalidOperationException(
                    "Tool failed"
                )
            );

        toolRegistry
            .Setup(x => x.GetAll())
            .Returns(
                new[]
                {
                    tool.Object
                }
            );

        toolRegistry
            .Setup(x => x.GetRequired("failing_tool"))
            .Returns(tool.Object);

        var toolCall = new AgentModelToolCall(
            "call_1",
            "failing_tool",
            "{}"
        );

        var responses = new Queue<AgentModelResponse>();

        responses.Enqueue(
            new AgentModelResponse(
                new AgentModelMessage(
                    AgentModelRole.Assistant,
                    null,
                    new[]
                    {
                        toolCall
                    }
                ),
                new[]
                {
                    toolCall
                }
            )
        );

        responses.Enqueue(
            new AgentModelResponse(
                new AgentModelMessage(
                    AgentModelRole.Assistant,
                    "Handled failure"
                ),
                Array.Empty<AgentModelToolCall>()
            )
        );

        modelClient
            .Setup(x => x.SendAsync(
                It.IsAny<IReadOnlyList<AgentModelMessage>>(),
                It.IsAny<IReadOnlyCollection<IAgentTool>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync(() => responses.Dequeue());

        responseValidator
            .Setup(x => x.ValidateAsync(
                "Handled failure",
                It.IsAny<IReadOnlyList<AgentToolExecution>>(),
                It.IsAny<CancellationToken>()
            ))
            .ReturnsAsync("Handled failure");

        var runner = CreateRunner(
            modelClient.Object,
            toolRegistry.Object,
            sessionRepository.Object,
            executionContext.Object,
            responseValidator.Object
        );

        var result = await runner.RunAsync(
            "Run failing tool",
            10.7769,
            106.7009,
            null,
            Guid.NewGuid()
        );

        Assert.Equal(
            "Handled failure",
            result.Answer
        );

        Assert.NotNull(createdSession);

        var persistedToolCall =
            Assert.Single(
                createdSession!.ToolCalls
            );

        Assert.Equal(
            AgentToolCallStatus.Failed,
            persistedToolCall.Status
        );

        Assert.Equal(
            "Tool failed",
            persistedToolCall.Error
        );
    }

    private static AgentRunner CreateRunner(
        IAgentModelClient modelClient,
        IAgentToolRegistry toolRegistry,
        IAgentSessionRepository sessionRepository,
        IAgentExecutionContext executionContext,
        IAgentResponseValidator responseValidator
    )
    {
        return new AgentRunner(
            modelClient,
            toolRegistry,
            sessionRepository,
            executionContext,
            responseValidator,
            Options.Create(
                new AgentRunnerOptions
                {
                    MaxSteps = 8
                }
            ),
            NullLogger<AgentRunner>.Instance
        );
    }
}
