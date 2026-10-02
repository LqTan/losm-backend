using System.Text.Json;
using AgentCore.Application.Abstractions;

namespace AgentCore.Application.Agent.Queries.GetAgentSessionHistory;

public sealed class GetAgentSessionHistoryHandler
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IAgentSessionRepository _sessionRepository;
    private readonly ILastSearchContextStore _lastSearchContextStore;

    public GetAgentSessionHistoryHandler(
        IAgentSessionRepository sessionRepository,
        ILastSearchContextStore lastSearchContextStore)
    {
        _sessionRepository = sessionRepository;
        _lastSearchContextStore = lastSearchContextStore;
    }

    public async Task<GetAgentSessionHistoryResult?> HandleAsync(
        GetAgentSessionHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(
            query.SessionId,
            cancellationToken);

        if (session is null || session.UserId != query.UserId)
        {
            return null;
        }

        var messages = session.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AgentMessageHistoryResult(
                x.Id,
                x.Role.ToString(),
                x.Content,
                x.CreatedAt))
            .ToList();

        var toolCalls = session.ToolCalls
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AgentToolCallHistoryResult(
                x.Id,
                x.ToolName,
                x.ArgumentsJson,
                x.ResultJson,
                x.Error,
                x.Status.ToString(),
                x.CreatedAt,
                x.CompletedAt))
            .ToList();

        var lastContext = await _lastSearchContextStore.GetAsync(
            session.Id, cancellationToken);

        LastSearchContextSnapshot? snapshot = null;
        if (lastContext is not null)
        {
            snapshot = new LastSearchContextSnapshot(
                lastContext.Query,
                lastContext.CenterLatitude,
                lastContext.CenterLongitude,
                lastContext.RadiusKm,
                ParsePlaceIds(lastContext.ResultPlaceIdsJson),
                lastContext.FiltersJson,
                ParseAttachedPlaces(lastContext.AttachedPlacesJson),
                lastContext.UpdatedAt);
        }

        return new GetAgentSessionHistoryResult(
            session.Id,
            session.CreatedAt,
            session.UpdatedAt,
            messages,
            toolCalls,
            snapshot);
    }

    private static IReadOnlyList<Guid> ParsePlaceIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions)
                ?? new List<Guid>();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<LastSearchAttachedPlace> ParseAttachedPlaces(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<LastSearchAttachedPlace>>(
                json, JsonOptions) ?? new List<LastSearchAttachedPlace>();
        }
        catch
        {
            return [];
        }
    }
}
