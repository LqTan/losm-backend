using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Transit.Application.Network.Queries;

namespace Transit.Presentation;

[ApiController]
[Route("api/transit")]
[AllowAnonymous]
public sealed class TransitController(TransitQueryHandler queryHandler) : ControllerBase
{
    [HttpGet("journey")]
    public async Task<IActionResult> GetJourney(
        [FromQuery] double originLat,
        [FromQuery] double originLon,
        [FromQuery] double destLat,
        [FromQuery] double destLon,
        [FromQuery] string? originName = null,
        [FromQuery] string? destName = null,
        [FromQuery] string? mode = null,
        [FromQuery] double radiusMeters = 850,
        CancellationToken ct = default)
    {
        var origin = new TransitLocation(originName ?? "Origin", originLat, originLon);
        var dest = new TransitLocation(destName ?? "Destination", destLat, destLon);

        var query = new TransitQuery(
            Kind: "journeys",
            Mode: mode,
            Origin: origin,
            Destination: dest,
            RadiusMeters: radiusMeters,
            Limit: 5
        );

        var result = await queryHandler.HandleAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("routes")]
    public async Task<IActionResult> GetRoutes(
        [FromQuery] string? query = null,
        [FromQuery] int? number = null,
        [FromQuery] string? mode = null,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var q = new TransitQuery(
            Kind: "routes",
            Mode: mode,
            Query: query,
            RouteNumber: number,
            Limit: limit
        );

        var result = await queryHandler.HandleAsync(q, ct);
        return Ok(result);
    }
}
