using System.Security.Claims;
using AgentCore.Infrastructure.PromptsManagement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentCore.Presentation.Controllers;

[ApiController]
[Route("api/admin/prompts")]
[Authorize(Policy = "AdminOnly")]
public sealed class PromptsAdminController(PromptCatalog catalog, IPromptStore store, PromptCache cache) : ControllerBase
{
    public sealed record PublishRequest(string Content, long Version);
    public sealed record PreviewRequest(string Content, Dictionary<string, string>? Variables = null);

    [HttpGet]
    public IActionResult List() => Ok(new { persistent = true, storage = "sqlserver",
        prompts = catalog.GetAll().Select(p => new { p.Key, p.Group, p.Variables, p.SampleVariables }) });

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var definition = catalog.GetAll().FirstOrDefault(p => p.Key == key);
        if (definition is null) return NotFound();
        var doc = await store.GetAsync(key, ct);
        var content = doc is null ? definition.DefaultContent : doc.Content;
        return Ok(new { definition.Key, definition.Variables, definition.DefaultContent,
            content, version = doc?.Version ?? 0, updatedAt = doc?.ChangedAt,
            sampleVariables = definition.SampleVariables });
    }

    [HttpPost("{key}/preview")]
    public IActionResult Preview(string key, PreviewRequest request)
    {
        var definition = catalog.GetAll().FirstOrDefault(p => p.Key == key);
        if (definition is null) return NotFound();
        try { PromptCatalog.Validate(definition, request.Content); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }

        var userVars = request.Variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var effectiveValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in definition.Variables)
        {
            if (userVars.TryGetValue(v, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                effectiveValues[v] = val;
            }
            else if (definition.SampleVariables is not null && definition.SampleVariables.TryGetValue(v, out var sample) && !string.IsNullOrWhiteSpace(sample))
            {
                effectiveValues[v] = sample;
            }
            else
            {
                effectiveValues[v] = $"[Mẫu: {v}]";
            }
        }

        var content = ManagedTemplates.Render(definition, request.Content,
            effectiveValues.Select(v => (v.Key, v.Value)).ToArray());
        return Ok(new { content });
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Publish(string key, PublishRequest request, CancellationToken ct)
    {
        var definition = catalog.GetAll().FirstOrDefault(p => p.Key == key);
        if (definition is null) return NotFound();
        try { PromptCatalog.Validate(definition, request.Content); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var revision = await store.PublishAsync(key, request.Content, request.Version, userId, ct);
        if (revision is null)
            return Conflict(new { message = "This prompt has changed. Reload it before publishing." });
        await cache.InvalidateAsync(key);
        return Ok(new { version = revision.Version });
    }

    [HttpGet("{key}/history")]
    public async Task<IActionResult> History(string key, CancellationToken ct)
    {
        if (!catalog.GetAll().Any(p => p.Key == key)) return NotFound();
        var changes = await store.HistoryAsync(key, ct);
        return Ok(changes.Select(c => new
        {
            c.Version, c.ChangedAt, c.ChangedBy, c.Content
        }));
    }
}
