using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SearchPlaces;
using Configuration.Application.Abstractions;
using Search.Application.Search.Queries.SearchPlaces;

namespace AgentCore.Infrastructure.Tools;

public sealed class SearchPlacesTool
    : AgentTool<SearchPlacesToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SearchPlacesHandler _searchPlacesHandler;
    private readonly IAgentExecutionContext _executionContext;
    private readonly ITuningProvider _tuning;

    public SearchPlacesTool(
        SearchPlacesHandler searchPlacesHandler,
        IAgentExecutionContext executionContext,
        ITuningProvider tuning) : base(tuning, "search_places")
    {
        _searchPlacesHandler = searchPlacesHandler;
        _executionContext = executionContext;
        _tuning = tuning;
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

        var radiusKm = arguments.RadiusKm > 0
            ? arguments.RadiusKm
            : opts.DefaultRadiusKm;

        var limit = arguments.Limit > 0 ? arguments.Limit : opts.DefaultLimit;
        if (limit > opts.MaxLimit) limit = opts.MaxLimit;

        var query = new SearchPlacesQuery(
            arguments.Query,
            _executionContext.Latitude,
            _executionContext.Longitude,
            radiusKm
        );

        var results = await _searchPlacesHandler.HandleAsync(
            query,
            cancellationToken
        );

        return JsonSerializer.Serialize(
            results.Take(limit),
            JsonOptions
        );
    }
}
