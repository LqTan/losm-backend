using System.Text.Json;
using Configuration.Application.Admin.Commands.DeleteConfiguration;
using Configuration.Application.Admin.Commands.UpsertConfiguration;
using Configuration.Application.Admin.Queries.GetConfiguration;
using Configuration.Application.Admin.Queries.ListConfigurations;
using Configuration.Application.Abstractions;
using Configuration.Presentation.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Configuration.Presentation.Controllers;

[ApiController]
[Route("api/admin/configurations")]
[Authorize(Policy = "AdminOnly")]
public sealed class ConfigurationAdminController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListConfigurationsHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
        return Ok(result);
    }

    [HttpGet("{scope}")]
    public async Task<IActionResult> Get(
        string scope,
        [FromServices] GetConfigurationHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new GetConfigurationQuery(scope), ct);
        if (result is null) return NotFound();
        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(result);
    }

    [HttpGet("{scope}/history")]
    public async Task<IActionResult> History(
        string scope,
        [FromServices] IConfigurationStore store,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var history = await store.GetHistoryAsync(scope, Math.Clamp(limit, 1, 500), ct);
        return Ok(history);
    }

    [HttpPut("{scope}")]
    public async Task<IActionResult> Upsert(
        string scope,
        [FromBody] UpsertConfigurationApiRequest body,
        [FromServices] UpsertConfigurationHandler handler,
        [FromServices] ITuningProvider tuning,
        CancellationToken ct)
    {
        var ifMatch = Request.Headers.IfMatch.ToString().Trim('"');
        long? expectedVersion = long.TryParse(ifMatch, out var v) ? v : null;

        var valueJson = JsonSerializer.Serialize(body.Value);

        var result = await handler.HandleAsync(new UpsertConfigurationCommand(
            scope,
            body.Module,
            body.Key,
            valueJson,
            expectedVersion,
            null), ct);

        tuning.InvalidateScope(scope);

        if (!result.Succeeded && result.Error?.Contains("ETag") == true)
        {
            Response.Headers.ETag = $"\"{result.Version}\"";
            return StatusCode(StatusCodes.Status412PreconditionFailed, result);
        }

        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(result);
    }

    [HttpDelete("{scope}")]
    public async Task<IActionResult> Delete(
        string scope,
        [FromBody] DeleteConfigurationApiRequest body,
        [FromServices] DeleteConfigurationHandler handler,
        [FromServices] ITuningProvider tuning,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new DeleteConfigurationCommand(
            scope, body.Version, null), ct);

        tuning.InvalidateScope(scope);
        return result.Succeeded ? NoContent() : Conflict(result);
    }
}
