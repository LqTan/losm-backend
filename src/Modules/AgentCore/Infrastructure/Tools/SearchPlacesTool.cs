using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.SearchPlaces;
using Search.Application.Search.Queries.SearchPlaces;

namespace AgentCore.Infrastructure.Tools;

public sealed class SearchPlacesTool
    : AgentTool<SearchPlacesToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly SearchPlacesHandler _searchPlacesHandler;
    private readonly IAgentExecutionContext _executionContext;

    public SearchPlacesTool(
        SearchPlacesHandler searchPlacesHandler,
        IAgentExecutionContext executionContext
    )
    {
        _searchPlacesHandler = searchPlacesHandler;
        _executionContext = executionContext;
    }

    public override string Name => "search_places";

    public override string Description =>
        "Search and rank places near the current user's location.";

    protected override async Task<string> ExecuteAsync(
        SearchPlacesToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(arguments.Query))
        {
            throw new ArgumentException(
                "Query is required."
            );
        }

        if (arguments.RadiusKm <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arguments.RadiusKm)
            );
        }

        if (arguments.Limit is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(arguments.Limit)
            );
        }

        var query = new SearchPlacesQuery(
            arguments.Query,
            _executionContext.Latitude,
            _executionContext.Longitude,
            arguments.RadiusKm
        );

        var results = await _searchPlacesHandler.HandleAsync(
            query,
            cancellationToken
        );

        return JsonSerializer.Serialize(
            results.Take(arguments.Limit),
            JsonOptions
        );
    }
}
