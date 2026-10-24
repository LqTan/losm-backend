using AgentCore.Infrastructure.PromptsManagement;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using AgentCore.Domain.Entities;
using AgentCore.Domain.Enums;
using AgentCore.Infrastructure.Services;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.AgentRuntime;

public sealed class AgentRunner : IAgentRunner
{
    private readonly PromptProvider? _prompts;
    private readonly IAgentModelClient _modelClient;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IAgentSessionRepository _sessionRepository;
    private readonly IAgentExecutionContext _executionContext;
    private readonly IAgentResponseValidator _responseValidator;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly ILastSearchContextStore _lastSearchContextStore;
    private readonly ITuningProvider _tuning;
    private readonly ILogger<AgentRunner> _logger;
    private readonly Areas.Application.Abstractions.IAreaRepository? _areaRepository;

    public AgentRunner(
        IAgentModelClient modelClient,
        IAgentToolRegistry toolRegistry,
        IAgentSessionRepository sessionRepository,
        IAgentExecutionContext executionContext,
        IAgentResponseValidator responseValidator,
        IPendingActionStore pendingActionStore,
        ILastSearchContextStore lastSearchContextStore,
        ITuningProvider tuning,
        ILogger<AgentRunner> logger,
        Areas.Application.Abstractions.IAreaRepository? areaRepository = null,
        PromptProvider? prompts = null
    )
    {
        _prompts = prompts;
        _modelClient = modelClient;
        _toolRegistry = toolRegistry;
        _sessionRepository = sessionRepository;
        _executionContext = executionContext;
        _responseValidator = responseValidator;
        _pendingActionStore = pendingActionStore;
        _lastSearchContextStore = lastSearchContextStore;
        _tuning = tuning;
        _logger = logger;
        _areaRepository = areaRepository;
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
        _executionContext.SetLocation(latitude, longitude);

        var runtimeOptions = await _tuning.GetRuntimeAsync(cancellationToken);
        var maxSteps = runtimeOptions.MaxSteps > 0 ? runtimeOptions.MaxSteps : 8;

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

        session.AddMessage(AgentMessageRole.User, input);

        await _sessionRepository.UpdateAsync(session, cancellationToken);

        var messages = BuildModelMessages(session);
        var agentPrompt = PromptFile.Load("agent-system.txt");
        if (_prompts is not null) agentPrompt = await _prompts.ResolveAsync("agent-system", agentPrompt, cancellationToken);
        messages.Insert(0, new AgentModelMessage(AgentModelRole.System, agentPrompt));

        if (_areaRepository != null)
        {
            try
            {
                var allAreas = await _areaRepository.GetAllAsync(cancellationToken);
                var activeAreaNames = allAreas.Where(a => a.IsActive).Select(a => a.Name).ToList();
                if (activeAreaNames.Count > 0)
                {
                    var areasDesc = string.Join(", ", activeAreaNames);
                    var areaContextPrompt = PromptFile.Load("area-context-system.txt");
                    if (_prompts is not null)
                        areaContextPrompt = await _prompts.ResolveAsync(
                            "area-context-system", areaContextPrompt, cancellationToken);
                    messages.Insert(
                        0,
                        new AgentModelMessage(
                            AgentModelRole.System,
                            areaContextPrompt.Replace("{areasDesc}", areasDesc)
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load active areas for AgentRunner dynamic context.");
            }
        }

        if (!string.IsNullOrWhiteSpace(approvedPlan))
        {
            var approvedPlanSystemPrompt = PromptFile.Load("approved-plan-system.txt");
            if (_prompts is not null) approvedPlanSystemPrompt = await _prompts.ResolveAsync("approved-plan-system", approvedPlanSystemPrompt, cancellationToken);
            var injectedPrompt =
                approvedPlanSystemPrompt.Replace("{approvedPlan}", approvedPlan);

            messages.Insert(
                0,
                new AgentModelMessage(AgentModelRole.System, injectedPrompt)
            );
        }

        if (session.Id != Guid.Empty)
        {
            var lastContext = await _lastSearchContextStore.GetAsync(
                session.Id, cancellationToken);

            if (lastContext is not null)
            {
                var contextJson = JsonSerializer.Serialize(new
                {
                    lastQuery = lastContext.Query,
                    center = new
                    {
                        latitude = lastContext.CenterLatitude,
                        longitude = lastContext.CenterLongitude
                    },
                    radiusKm = lastContext.RadiusKm,
                    resultPlaceIds = lastContext.ResultPlaceIdsJson,
                    filters = ParseFilters(lastContext.FiltersJson)
                });

                messages.Insert(
                    0,
                    new AgentModelMessage(
                        AgentModelRole.System,
                        "Recent structured search context for this session (use to interpret follow-up turns):\n" +
                        contextJson
                    )
                );
            }
        }

        messages.Insert(0, new AgentModelMessage(AgentModelRole.System, PromptFile.Load("transit-system.txt")));
        var tools = _toolRegistry.GetAll();

        var toolExecutions = new List<AgentToolExecution>();
        var activity = new List<AgentActivityStep>();
        var order = new Counter();
        var attachedPlaces = new List<AttachedPlace>();
        var attachedPlaceIds = new HashSet<Guid>();
        AttachedRoute? attachedRoute = null;
        var transitResults = new List<Transit.Application.Network.Queries.TransitQueryResult>();
        AgentMeetingResults? meetingResults = null;

        for (var step = 0; step < maxSteps; step++)
        {
            _logger.LogDebug(
                "Agent step {Step} started. SessionId: {SessionId}",
                step + 1,
                session.Id
            );

            AgentModelResponse response;
            try
            {
                response = await _modelClient.SendAsync(
                    messages,
                    tools,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Agent model invocation failed. SessionId: {SessionId}",
                    session.Id
                );

                var errorAnswer = await ManagedTemplates.RenderAsync(_prompts, "message.agent.unavailable", cancellationToken);
                session.AddMessage(AgentMessageRole.Assistant, errorAnswer);
                await _sessionRepository.UpdateAsync(session, cancellationToken);

                await RecordStep(
                    activity, sink, order,
                    AgentActivityStepKind.Finalize, null, false);

                return new AgentRunResult(
                    session.Id,
                    errorAnswer,
                    activity,
                    [],
                    attachedPlaces,
                    attachedRoute,
                    transitResults,
                    meetingResults
                );
            }

            messages.Add(response.AssistantMessage);

            if (!response.HasToolCalls)
            {
                var answer = response.AssistantMessage.Content;

                if (string.IsNullOrWhiteSpace(answer))
                {
                    throw new InvalidOperationException(
                        "Agent model returned an empty response."
                    );
                }

                var validatedAnswer = answer;
                try
                {
                    validatedAnswer = await _responseValidator.ValidateAsync(
                        answer,
                        toolExecutions,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Response validation failed for SessionId: {SessionId}. Falling back to unvalidated answer.",
                        session.Id
                    );
                }

                session.AddMessage(AgentMessageRole.Assistant, validatedAnswer);

                await _sessionRepository.UpdateAsync(session, cancellationToken);

                _logger.LogInformation(
                    "Agent run completed. SessionId: {SessionId}, Steps: {Steps}",
                    session.Id,
                    step + 1
                );

                await RecordStep(
                    activity, sink, order,
                    AgentActivityStepKind.Finalize, null, true);

                var pending = await _pendingActionStore
                    .GetBySessionAsync(session.Id, cancellationToken);

                return new AgentRunResult(
                    session.Id,
                    validatedAnswer,
                    activity,
                    pending,
                    attachedPlaces,
                    attachedRoute,
                    transitResults,
                    meetingResults
                );
            }

            foreach (var toolCall in response.ToolCalls)
            {
                var tool = _toolRegistry.GetRequired(toolCall.Name);

                await RecordStep(
                    activity, sink, order,
                    AgentActivityStepKind.ToolCall, toolCall.Name, true);

                var execution = await ExecuteToolCallAsync(
                    session, messages, toolCall, cancellationToken);

                if (execution.Succeeded &&
                    !string.IsNullOrWhiteSpace(execution.Result))
                {
                    var extracted = AttachedPlaceExtractor.Extract(
                        tool, execution.Result, attachedPlaceIds);

                    foreach (var place in extracted)
                    {
                        attachedPlaceIds.Add(place.PlaceId);
                        attachedPlaces.Add(place);
                    }

                    if (tool.Name is "search_transit_stops" or "get_transit_routes" or "find_transit_journey")
                    {
                        try
                        {
                            var transit = JsonSerializer.Deserialize<Transit.Application.Network.Queries.TransitQueryResult>(
                                execution.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                            if (transit is not null) transitResults.Add(transit);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse TransitQueryResult from {ToolName} result.", tool.Name);
                        }
                    }

                    if (tool.Name == "search_meeting_places")
                    {
                        try
                        {
                            meetingResults = JsonSerializer.Deserialize<AgentMeetingResults>(
                                execution.Result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse AgentMeetingResults from search_meeting_places result.");
                        }
                    }

                    if (tool.Name == "get_directions")
                    {
                        try
                        {
                            using var routeDoc = JsonDocument.Parse(execution.Result);
                            if (routeDoc.RootElement.TryGetProperty("route", out var routeProp) &&
                                routeProp.ValueKind == JsonValueKind.Object)
                            {
                                attachedRoute = JsonSerializer.Deserialize<AttachedRoute>(
                                    routeProp.GetRawText(),
                                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse AttachedRoute from get_directions result.");
                        }
                    }
                }

                await RecordStep(
                    activity, sink, order,
                    AgentActivityStepKind.ToolResult, toolCall.Name, execution.Succeeded);

                if (execution.Succeeded &&
                    tool.Kind == ToolKind.WriteRequiresConfirmation)
                {
                    await RecordStep(
                        activity, sink, order,
                        AgentActivityStepKind.PendingAction, toolCall.Name, true);
                }

                toolExecutions.Add(execution);
            }
        }

        _logger.LogWarning(
            "Agent reached maximum steps. SessionId: {SessionId}, MaxSteps: {MaxSteps}",
            session.Id,
            maxSteps
        );

        var fallbackAnswer = transitResults.Count > 0 ? transitResults[^1].Message : attachedPlaces.Count > 0
            ? await ManagedTemplates.RenderAsync(_prompts, "message.agent.results", cancellationToken, ("placeNames", $"{string.Join(", ", attachedPlaces.Take(3).Select(p => p.Name))}"))
            : await ManagedTemplates.RenderAsync(_prompts, "message.agent.empty", cancellationToken);

        session.AddMessage(AgentMessageRole.Assistant, fallbackAnswer);
        await _sessionRepository.UpdateAsync(session, cancellationToken);

        await RecordStep(
            activity, sink, order,
            AgentActivityStepKind.Finalize, null, true);

        var pendingActions = await _pendingActionStore
            .GetBySessionAsync(session.Id, cancellationToken);

        return new AgentRunResult(
            session.Id,
            fallbackAnswer,
            activity,
            pendingActions,
            attachedPlaces,
            attachedRoute,
            transitResults,
            meetingResults
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
            var existingSession = await _sessionRepository.GetByIdAsync(
                sessionId.Value, cancellationToken);

            if (existingSession is null)
            {
                _logger.LogWarning(
                    "Agent session {SessionId} not found; starting a new session for user {UserId}",
                    sessionId.Value,
                    userId);

                var newSession = new AgentSession(Guid.NewGuid(), userId);
                await _sessionRepository.AddAsync(newSession, cancellationToken);
                return newSession;
            }

            if (existingSession.UserId != userId)
            {
                throw new KeyNotFoundException(
                    $"Agent session '{sessionId.Value}' was not found.");
            }

            return existingSession;
        }

        var session = new AgentSession(Guid.NewGuid(), userId);

        await _sessionRepository.AddAsync(session, cancellationToken);

        return session;
    }

    private static Dictionary<string, object?> ParseFilters(string filtersJson)
    {
        if (string.IsNullOrWhiteSpace(filtersJson) ||
            filtersJson == "{}")
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            var node = JsonNode.Parse(filtersJson);
            if (node is not JsonObject obj)
            {
                return new Dictionary<string, object?>();
            }

            var result = new Dictionary<string, object?>();
            foreach (var kv in obj)
            {
                result[kv.Key] = kv.Value is null
                    ? null
                    : JsonSerializer.Deserialize<object?>(kv.Value.ToJsonString());
            }
            return result;
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static List<AgentModelMessage> BuildModelMessages(AgentSession session)
    {
        return session.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(MapMessage)
            .ToList();
    }

    private static AgentModelMessage MapMessage(AgentMessage message)
    {
        var role = message.Role switch
        {
            AgentMessageRole.User => AgentModelRole.User,
            AgentMessageRole.Assistant => AgentModelRole.Assistant,
            _ => throw new InvalidOperationException(
                $"Unsupported agent message role: {message.Role}")
        };

        return new AgentModelMessage(role, message.Content);
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

        var persistedToolCall = session.StartToolCall(
            toolCall.Name,
            toolCall.ArgumentsJson
        );

        await _sessionRepository.UpdateAsync(session, cancellationToken);

        string toolResult;
        var succeeded = false;

        try
        {
            var tool = _toolRegistry.GetRequired(toolCall.Name);

            using var argumentsDocument = JsonDocument.Parse(toolCall.ArgumentsJson);

            toolResult = await tool.ExecuteAsync(
                argumentsDocument.RootElement,
                cancellationToken
            );

            session.CompleteToolCall(persistedToolCall.Id, toolResult);
            succeeded = true;

            _logger.LogInformation(
                "Agent tool completed. SessionId: {SessionId}, Tool: {ToolName}",
                session.Id,
                toolCall.Name
            );
        }
        catch (Exception exception)
        {
            toolResult = CreateToolError(exception.Message);

            session.FailToolCall(persistedToolCall.Id, exception.Message);

            _logger.LogError(
                exception,
                "Agent tool failed. SessionId: {SessionId}, Tool: {ToolName}",
                session.Id,
                toolCall.Name
            );
        }

        await _sessionRepository.UpdateAsync(session, cancellationToken);

        messages.Add(new AgentModelMessage(
            AgentModelRole.Tool, toolResult,
            ToolCallId: toolCall.Id));

        return new AgentToolExecution(toolCall.Name, toolResult, succeeded);
    }

    private static string CreateToolError(string message)
    {
        return JsonSerializer.Serialize(
            new Dictionary<string, string> { ["error"] = message }
        );
    }

    private async Task RecordStep(
        List<AgentActivityStep> activity,
        IAgentEventSink? sink,
        Counter order,
        AgentActivityStepKind kind,
        string? toolName,
        bool succeeded
    )
    {
        string? summary = null;
        if (!string.IsNullOrWhiteSpace(toolName))
        {
            try
            {
                var tool = _toolRegistry.GetRequired(toolName);
                summary = kind switch
                {
                    AgentActivityStepKind.ToolCall => StepSummary.ForToolCall(tool),
                    AgentActivityStepKind.ToolResult => StepSummary.ForToolResult(tool, succeeded),
                    AgentActivityStepKind.PendingAction => StepSummary.ForPendingAction(tool),
                    _ => null
                };
            }
            catch
            {
                // ignore
            }
        }

        summary ??= kind switch
        {
            AgentActivityStepKind.Finalize => succeeded
                ? StepSummary.Finalize
                : StepSummary.FinalizeFailed,
            AgentActivityStepKind.ModelResponse => StepSummary.Thinking,
            _ => succeeded ? "Processing..." : "Processing failed"
        };

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
            await sink.EmitStepAsync(step, CancellationToken.None);
        }
    }

    private sealed class Counter
    {
        private int _value;
        public int Next() => _value++;
    }
}
