using Microsoft.AspNetCore.Mvc;
using Search.Application.Search.Queries.SearchMeetingPlaces;
using Search.Application.Search.Queries.SearchPlaces;
using SearchPlacesResult = Search.Application.Search.Queries.SearchPlaces.SearchResult;

namespace Search.Presentation.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly SearchPlacesHandler _handler;
    private readonly SearchMeetingPlacesHandler _meetingHandler;

    public SearchController(
        SearchPlacesHandler handler,
        SearchMeetingPlacesHandler meetingHandler)
    {
        _handler = handler;
        _meetingHandler = meetingHandler;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchPlacesResult>>> Search(
        [FromQuery] string query,
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double? radiusKm = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _handler.HandleAsync(
            new SearchPlacesQuery(query, latitude, longitude, radiusKm),
            cancellationToken
        );
        return Ok(result);
    }

    [HttpPost("meeting-places")]
    public async Task<ActionResult<SearchMeetingPlacesResult>> SearchMeetingPlaces(
        [FromBody] SearchMeetingPlacesRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _meetingHandler.HandleAsync(
            new SearchMeetingPlacesQuery(
                request.Query,
                request.Origins.Select(o => new SearchMeetingPlaceOrigin(
                    o.Name, o.Latitude, o.Longitude)).ToList(),
                request.RadiusKm,
                request.TopK),
            cancellationToken
        );
        return Ok(result);
    }
}

public sealed record SearchMeetingPlaceRequest(
    string? Name,
    double Latitude,
    double Longitude
);

public sealed record SearchMeetingPlacesRequest(
    string Query,
    IReadOnlyList<SearchMeetingPlaceRequest> Origins,
    double? RadiusKm = null,
    int? TopK = null
);
