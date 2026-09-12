using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentCore.Infrastructure.AgentRuntime;

public sealed class AgentRunner : IAgentRunner
{
    private readonly IAgentModelClient _modelClient;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentExecutionContext _executionContext;
    private readonly IAgentResponseValidator _responseValidator;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly AgentRunnerOptions _options;
    private readonly ILogger<AgentRunner> _logger;

    public AgentRunner(
        IAgentModelClient modelClient,
        IAgentToolRegistry toolRegistry,
        IAgentSessionRepository sessionRepository,
        IAgentExecutionContext executionContext,
        IAgentResponseValidator responseValidator,
        IPendingActionStore pendingActionStore,
        IOptions<AgentRunnerOptions> options,
        ILogger<AgentRunner> logger
    )
    {
        _modelClient = modelClient;
        _toolRegistry = toolRegistry;
        _sessionRepository = sessionRepository;
        _executionContext = executionContext;
        _responseValidator = responseValidator;
        _pendingActionStore = pendingActionStore;
        _options = options.Value;
        _logger = logger;
    }

    public Task<AgentRunResult> RunAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        CancellationToken cancellationToken = default
    )
    {
        return RunInternalAsync(
            input,
            latitude,
            longitude,
            sessionId,
            userId,
            null,
            null,
            cancellationToken
        );
    }

    public Task<AgentRunResult> RunApprovedPlanAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        string approvedPlan,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(approvedPlan))
        {
            throw new ArgumentException(
                "Approved plan is required.",
                nameof(approvedPlan)
            );
        }

        return RunInternalAsync(
            input,
            latitude,
            longitude,
            sessionId,
            userId,
            approvedPlan,
            null,
            cancellationToken
        );
    }

    public async Task<AgentRunResult> RunStreamedAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        IAgentEventSink sink,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await RunInternalAsync(
                input,
                latitude,
                longitude,
                sessionId,
                userId,
                null,
                sink,
                cancellationToken
            );

            await sink.EmitResultAsync(result, cancellationToken);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Streamed agent run failed. UserId: {UserId}",
                userId
            );
            await sink.EmitErrorAsync(
                ex.Message,
                cancellationToken
            );
            throw;
        }
    }

    private async Task<AgentRunResult> RunInternalAsync(
        string input,
        double latitude,
        double longitude,
        Guid? sessionId,
        Guid? userId,
        string? approvedPlan,
        IAgentEventSink? sink,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException(
                "Agent input is required.",
                nameof(input)
            );
        }

        _executionContext.SetUser(userId);

        _executionContext.SetLocation(
            latitude,
            longitude
        );

        var session = await GetOrCreateSessionAsync(
            sessionId,
            userId,
            cancellationToken
        );

        _executionContext.SetSession(session.Id);

        _logger.LogInformation(
            "Agent run started. SessionId: {SessionId}, UserId: {UserId}",
            session.Id,
            userId
        );

        session.AddMessage(
            AgentMessageRole.User,
            input
        );

        await _sessionRepository.UpdateAsync(
            session,
            cancellationToken
        );

        var messages = BuildModelMessages(session);

        if (!string.IsNullOrWhiteSpace(approvedPlan))
        {
            messages.Insert(
                0,
                new AgentModelMessage(
                    AgentModelRole.System,
                    $"""
                    Execute the user's request according to the approved plan below.

                    Do not execute work outside this plan.
                    Use available tools when required.
                    If evidence is unavailable, do not invent it.

                    APPROVED PLAN:
                    {approvedPlan}
                    """
                )
            );
        }

        var tools = _toolRegistry.GetAll();

        var toolExecutions =
            new List<AgentToolExecution>();

        var activity = new List<AgentActivityStep>();
        var order = new Counter();
        var attachedPlaces = new List<AttachedPlace>();
        var attachedPlaceIds = new HashSet<Guid>();

        for (var step = 0; step < _options.MaxSteps; step++)
        {
            _logger.LogDebug(
                "Agent step {Step} started. SessionId: {SessionId}",
                step + 1,
                session.Id
            );

            var response = await _modelClient.SendAsync(
                messages,
                tools,
                cancellationToken
            );

            messages.Add(
                response.AssistantMessage
            );

            if (!response.HasToolCalls)
            {
                var answer =
                    response.AssistantMessage.Content;

                if (string.IsNullOrWhiteSpace(answer))
                {
                    throw new InvalidOperationException(
                        "Agent model returned an empty response."
                    );
                }

                var validatedAnswer =
                    await _responseValidator.ValidateAsync(
                        answer,
                        toolExecutions,
                        cancellationToken
                    );

                session.AddMessage(
                    AgentMessageRole.Assistant,
                    validatedAnswer
                );

                await _sessionRepository.UpdateAsync(
                    session,
                    cancellationToken
                );

                _logger.LogInformation(
                    "Agent run completed. SessionId: {SessionId}, Steps: {Steps}",
                    session.Id,
                    step + 1
                );

await RecordStep(
                    activity,
                    sink,
                    order,
                    AgentActivityStepKind.Finalize,
                    null,
                    true
                );

                var pending =
                    _pendingActionStore.GetBySession(session.Id);

                return new AgentRunResult(
                    session.Id,
                    validatedAnswer,
                    activity,
                    pending,
                    attachedPlaces
                );
            }

            foreach (var toolCall in response.ToolCalls)
            {
await RecordStep(
                    activity,
                    sink,
                    order,
                    AgentActivityStepKind.ToolCall,
                    toolCall.Name,
                    true
                );

                var execution =
                    await ExecuteToolCallAsync(
                        session,
                        messages,
                        toolCall,
                        cancellationToken
                    );

                if (execution.Succeeded &&
                    !string.IsNullOrWhiteSpace(execution.Result))
                {
                    var extracted = AttachedPlaceExtractor.Extract(
                        toolCall.Name,
                        execution.Result,
                        attachedPlaceIds
                    );

                    foreach (var place in extracted)
                    {
                        attachedPlaceIds.Add(place.PlaceId);
                        attachedPlaces.Add(place);
                    }
                }

await RecordStep(
                    activity,
                    sink,
                    order,
                    AgentActivityStepKind.ToolResult,
                    toolCall.Name,
                    execution.Succeeded
                );

                if (execution.Succeeded &&
                    IsWriteActionTool(toolCall.Name))
                {
await RecordStep(
                        activity,
                        sink,
                        order,
                        AgentActivityStepKind.PendingAction,
                        toolCall.Name,
                        true
                    );
                }

                toolExecutions.Add(
                    execution
                );
            }
        }

        _logger.LogWarning(
            "Agent exceeded maximum steps. SessionId: {SessionId}, MaxSteps: {MaxSteps}",
            session.Id,
            _options.MaxSteps
        );

        throw new InvalidOperationException(
            $"Agent exceeded the maximum number of steps: {_options.MaxSteps}."
        );
    }

    private async Task<AgentSession> GetOrCreateSessionAsync(
        Guid? sessionId,
        Guid? userId,
        CancellationToken cancellationToken
    )
    {
        if (sessionId.HasValue)
        {
            var existingSession =
                await _sessionRepository.GetByIdAsync(
                    sessionId.Value,
                    cancellationToken
                );

            if (existingSession is null)
            {
                throw new KeyNotFoundException(
                    $"Agent session '{sessionId.Value}' was not found."
                );
            }

            if (existingSession.UserId != userId)
            {
                throw new KeyNotFoundException(
                    $"Agent session '{sessionId.Value}' was not found."
                );
            }

            return existingSession;
        }

        var session = new AgentSession(
            Guid.NewGuid(),
            userId
        );

        await _sessionRepository.AddAsync(
            session,
            cancellationToken
        );

        return session;
    }

    private static List<AgentModelMessage> BuildModelMessages(
        AgentSession session
    )
    {
        return session.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(MapMessage)
            .ToList();
    }

    private static AgentModelMessage MapMessage(
        AgentMessage message
    )
    {
        var role = message.Role switch
        {
            AgentMessageRole.User =>
                AgentModelRole.User,

            AgentMessageRole.Assistant =>
                AgentModelRole.Assistant,

            _ => throw new InvalidOperationException(
                $"Unsupported agent message role: {message.Role}"
            )
        };

        return new AgentModelMessage(
            role,
            message.Content
        );
    }

    private async Task<AgentToolExecution> ExecuteToolCallAsync(
        AgentSession session,
        List<AgentModelMessage> messages,
        AgentModelToolCall toolCall,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Agent tool started. SessionId: {SessionId}, Tool: {ToolName}",
            session.Id,
            toolCall.Name
        );

        var persistedToolCall =
            session.StartToolCall(
                toolCall.Name,
                toolCall.ArgumentsJson
            );

        await _sessionRepository.UpdateAsync(
            session,
            cancellationToken
        );

        string toolResult;

        var succeeded = false;

        try
        {
            var tool =
                _toolRegistry.GetRequired(
                    toolCall.Name
                );

            using var argumentsDocument =
                JsonDocument.Parse(
                    toolCall.ArgumentsJson
                );

            toolResult =
                await tool.ExecuteAsync(
                    argumentsDocument.RootElement,
                    cancellationToken
                );

            session.CompleteToolCall(
                persistedToolCall.Id,
                toolResult
            );

            succeeded = true;

            _logger.LogInformation(
                "Agent tool completed. SessionId: {SessionId}, Tool: {ToolName}",
                session.Id,
                toolCall.Name
            );
        }
        catch (Exception exception)
        {
            toolResult = CreateToolError(
                exception.Message
            );

            session.FailToolCall(
                persistedToolCall.Id,
                exception.Message
            );

            _logger.LogError(
                exception,
                "Agent tool failed. SessionId: {SessionId}, Tool: {ToolName}",
                session.Id,
                toolCall.Name
            );
        }

        await _sessionRepository.UpdateAsync(
            session,
            cancellationToken
        );

        messages.Add(
            new AgentModelMessage(
                AgentModelRole.Tool,
                toolResult,
                ToolCallId: toolCall.Id
            )
        );

        return new AgentToolExecution(
            toolCall.Name,
            toolResult,
            succeeded
        );
    }

    private static string CreateToolError(
        string message
    )
    {
        return JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                ["error"] = message
            }
        );
    }

    private static bool IsWriteActionTool(string toolName)
    {
        return toolName is "save_place" or "create_review";
    }

    private static async Task RecordStep(
        List<AgentActivityStep> activity,
        IAgentEventSink? sink,
        Counter order,
        AgentActivityStepKind kind,
        string? toolName,
        bool succeeded
    )
    {
        var summary = StepSummary.FromStep(
            kind,
            toolName,
            succeeded
        );

        var step = new AgentActivityStep(
            order.Next(),
            kind,
            toolName,
            summary,
            succeeded
        );

        activity.Add(step);

        if (sink is not null)
        {
            await sink.EmitStepAsync(
                step,
                CancellationToken.None
            );
        }
    }

    private sealed class Counter
    {
        private int _value;
        public int Next() => _value++;
    }
}
