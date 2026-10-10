using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Overture.Application.Contracts;
using Overture.Application.Jobs;
using Overture.Application.Settings.Commands.UpdateSetting;
using Overture.Application.Settings.Queries.GetSettings;
using Overture.Infrastructure;

namespace Overture.Presentation.Controllers;

[ApiController]
[Route("api/admin/overture")]
[Authorize(Roles = "Admin")]
public sealed class OvertureAdminController : ControllerBase
{
    private readonly UpdateSettingHandler _updateSetting;
    private readonly GetSettingsHandler _getSettings;
    private readonly IBackgroundJobClient _jobs;

    public OvertureAdminController(
        UpdateSettingHandler updateSetting,
        GetSettingsHandler getSettings,
        IBackgroundJobClient jobs)
    {
        _updateSetting = updateSetting;
        _getSettings = getSettings;
        _jobs = jobs;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> ListSettings(CancellationToken ct)
    {
        var result = await _getSettings.HandleAsync(ct);
        return Ok(result);
    }

    [HttpGet("settings/{key}")]
    public async Task<IActionResult> GetSetting(string key, CancellationToken ct)
    {
        var result = await _getSettings.HandleByKeyAsync(key, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("settings/{key}")]
    public async Task<IActionResult> UpdateSetting(
        string key,
        [FromBody] System.Text.Json.JsonElement body,
        [FromServices] OvertureRecurringJobBootstrap bootstrap,
        CancellationToken ct)
    {
        var rawJson = body.ValueKind == System.Text.Json.JsonValueKind.String
            ? body.GetString() ?? "{}"
            : body.GetRawText();

        var cmd = new UpdateSettingCommand(
            key,
            rawJson,
            null);
        var response = await _updateSetting.HandleAsync(cmd, ct);

        if (string.Equals(key, OvertureRecurringJobBootstrap.SettingsKey, StringComparison.OrdinalIgnoreCase))
        {
            await bootstrap.EnsureScheduledAsync(HttpContext.RequestServices, ct);
        }

        return Ok(response);
    }

    [HttpPost("import")]
    public IActionResult EnqueueImport([FromBody] OvertureImportRequest request)
    {
        if (request.AreaId == Guid.Empty)
            return BadRequest("areaId is required.");

        var batchSize = request.BatchSize <= 0 ? 5000 : request.BatchSize;

        var jobId = _jobs.Enqueue<ImportOverturePlacesJob>(job => job.RunAsync(
            new OvertureImportRequest(
                request.AreaId,
                batchSize,
                request.Bbox),
            CancellationToken.None));

        return Accepted(new OvertureImportResponse(
            jobId,
            request.AreaId,
            batchSize,
            DateTime.UtcNow));
    }
}