using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SearchPlaces;
using AgentCore.Domain.Entities;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Search.Application.Search.Queries.SearchPlaces;

namespace AgentCore.Infrastructure.Tools;

public sealed class SearchPlacesTool
    : AgentTool<SearchPlacesToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SearchPlacesHandler _searchPlacesHandler;
    private readonly IAgentExecutionContext _executionContext;
    private readonly ILastSearchContextStore _lastSearchContextStore;
    private readonly ITuningProvider _tuning;
    private readonly ILogger<SearchPlacesTool> _logger;

    public SearchPlacesTool(
        SearchPlacesHandler searchPlacesHandler,
        IAgentExecutionContext executionContext,
        ILastSearchContextStore lastSearchContextStore,
        ITuningProvider tuning,
        ILogger<SearchPlacesTool> logger) : base(tuning, "search_places")
    {
        _searchPlacesHandler = searchPlacesHandler;
        _executionContext = executionContext;
        _lastSearchContextStore = lastSearchContextStore;
        _tuning = tuning;
        _logger = logger;
    }

    public override string Name => "search_places";

    public override string Description =>
        "Search and rank places near the current user's location. " +
        "Use when the user asks to find places, recommendations, or anything nearby.";

    public override bool SuppliesPlaces => true;

    protected override async Task<string> ExecuteAsync(
        SearchPlacesToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(arguments.Query))
        {
            throw new ArgumentException("Query is required.");
        }

        var opts = await _tuning.GetToolOptionsAsync(Name, cancellationToken);
        var searchOptions = await _tuning.GetSearchOptionsAsync(cancellationToken);

        var radiusKm = arguments.RadiusKm > 0
            ? arguments.RadiusKm
            : opts.DefaultRadiusKm;

        var limit = arguments.Limit > 0 ? arguments.Limit : opts.DefaultLimit;
        if (limit > opts.MaxLimit) limit = opts.MaxLimit;

        var query = new SearchPlacesQuery(
            arguments.Query,
            _executionContext.Latitude,
            _executionContext.Longitude,
            radiusKm,
            searchOptions.CandidateLimit
        );

        var results = await _searchPlacesHandler.HandleAsync(
            query,
            cancellationToken
        );

        await TryUpsertLastSearchContextAsync(
            arguments.Query,
            radiusKm,
            results.Select(r => r.PlaceId),
            cancellationToken);

        return JsonSerializer.Serialize(
            results.Take(limit),
            JsonOptions
        );
    }

    private async Task TryUpsertLastSearchContextAsync(
        string query,
        double radiusKm,
        IEnumerable<Guid> resultPlaceIds,
        CancellationToken cancellationToken)
    {
        var sessionId = _executionContext.SessionId;
        if (!sessionId.HasValue || sessionId.Value == Guid.Empty) return;

        try
        {
            var placeIdsJson = JsonSerializer.Serialize(resultPlaceIds);
            var filtersJson = "{}";

            var existing = await _lastSearchContextStore.GetAsync(
                sessionId.Value, cancellationToken);

            if (existing is null)
            {
                var context = new LastSearchContext(
                    sessionId.Value,
                    query,
                    _executionContext.Latitude,
                    _executionContext.Longitude,
                    radiusKm,
                    placeIdsJson,
                    filtersJson);

                await _lastSearchContextStore.UpsertAsync(context, cancellationToken);
            }
            else
            {
                existing.Update(
                    query,
                    _executionContext.Latitude,
                    _executionContext.Longitude,
                    radiusKm,
                    placeIdsJson,
                    filtersJson);
                await _lastSearchContextStore.UpsertAsync(existing, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to upsert LastSearchContext for session {SessionId}",
                sessionId);
        }
    }
}
