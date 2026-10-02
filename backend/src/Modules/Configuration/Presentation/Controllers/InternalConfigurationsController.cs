using Configuration.Application.Admin.Queries.GetConfiguration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Configuration.Presentation.Controllers;

[ApiController]
[Route("api/internal/configurations")]
public sealed class InternalConfigurationsController : ControllerBase
{
    [HttpGet("{scope}")]
    public async Task<IActionResult> Get(
        string scope,
        [FromHeader(Name = "X-Service-Token")] string? serviceToken,
        [FromServices] IConfiguration config,
        [FromServices] GetConfigurationHandler handler,
        CancellationToken ct)
    {
        var expected = config["Elasticsearch:ServiceToken"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    status = 503,
                    message = "Internal configuration endpoint is not configured. Set Elasticsearch:ServiceToken to enable."
                });
        }

        if (!string.Equals(serviceToken, expected, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(new GetConfigurationQuery(scope), ct);
        if (result is null) return NotFound();
        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(result);
    }
}
