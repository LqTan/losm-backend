using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Areas.Application.Areas.Commands.CreateArea;
using Areas.Application.Areas.Commands.DeleteArea;
using Areas.Application.Areas.Commands.ImportAreas;
using Areas.Application.Areas.Commands.UpdateArea;
using Areas.Application.Areas.Queries.GetAreas;
using Areas.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Areas.Presentation.Controllers;

[ApiController]
[Route("api/areas")]
public sealed class AreasController : ControllerBase
{
    private readonly GetAreasHandler _get;
    private readonly CreateAreaHandler _create;
    private readonly UpdateAreaHandler _update;
    private readonly DeleteAreaHandler _delete;

    public AreasController(
        GetAreasHandler get,
        CreateAreaHandler create,
        UpdateAreaHandler update,
        DeleteAreaHandler delete)
    {
        _get = get;
        _create = create;
        _update = update;
        _delete = delete;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(
        [FromQuery] int? level,
        [FromQuery] Guid? parentId,
        CancellationToken ct)
    {
        var result = await _get.HandleAsync(level, parentId, ct);
        return Ok(result);
    }

    [HttpGet("tree")]
    [AllowAnonymous]
    public async Task<IActionResult> Tree(CancellationToken ct)
    {
        var result = await _get.HandleTreeAsync(ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateAreaCommand command,
        CancellationToken ct)
    {
        var result = await _create.HandleAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] Areas.Application.Abstractions.IAreaRepository repo,
        CancellationToken ct)
    {
        var area = await repo.GetByIdAsync(id, ct);
        if (area is null) return NotFound();
        return Ok(AreaResponse.FromEntity(area));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAreaCommand body,
        CancellationToken ct)
    {
        if (id != body.Id)
            return BadRequest("Id mismatch.");

        var result = await _update.HandleAsync(body, ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _delete.HandleAsync(id, ct);
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Import(
        IFormFile file,
        [FromServices] ImportAreasHandler handler,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File is required.");

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .json files are accepted.");

        await using var stream = file.OpenReadStream();
        var result = await handler.HandleAsync(stream, ct);
        return Ok(result);
    }

    [HttpGet("template")]
    [Authorize(Roles = "Admin")]
    public IActionResult Template()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        var path = Path.Combine(assemblyDir, "Seed", "vn-areas.json");

        if (!System.IO.File.Exists(path))
            return NotFound("Template file not found.");

        var bytes = System.IO.File.ReadAllBytes(path);
        return File(bytes, "application/json", "vn-areas.json");
    }
    [HttpGet("lookup-bbox")]
    [AllowAnonymous]
    public async Task<IActionResult> LookupBbox(
        [FromQuery] string query,
        [FromServices] IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Vui lòng nhập tên khu vực." });

        var trimmed = query.Trim();
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "LocationSearchApp/1.0 (contact: lequantan1974@gmail.com)");

            var searchQueries = new List<string> { trimmed };
            if (!trimmed.Contains("Việt Nam", StringComparison.OrdinalIgnoreCase) && !trimmed.Contains("Vietnam", StringComparison.OrdinalIgnoreCase))
            {
                searchQueries.Insert(0, $"{trimmed}, Hồ Chí Minh, Việt Nam");
                searchQueries.Insert(1, $"{trimmed}, Việt Nam");
            }

            foreach (var q in searchQueries)
            {
                var osmUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(q)}&format=jsonv2&limit=1&addressdetails=1&polygon_geojson=1";
                var res = await client.GetAsync(osmUrl, ct);
                if (!res.IsSuccessStatusCode) continue;

                var raw = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    if (item.TryGetProperty("boundingbox", out var bboxArr) && bboxArr.GetArrayLength() == 4)
                    {
                        var minLat = double.Parse(bboxArr[0].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                        var maxLat = double.Parse(bboxArr[1].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                        var minLng = double.Parse(bboxArr[2].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                        var maxLng = double.Parse(bboxArr[3].GetString()!, System.Globalization.CultureInfo.InvariantCulture);

                        var displayName = item.TryGetProperty("display_name", out var dn) ? dn.GetString() : q;
                        var placeName = item.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString())
                            ? n.GetString()!
                            : trimmed;

                        JsonElement? geoJson = null;
                        if (item.TryGetProperty("geojson", out var gj))
                        {
                            geoJson = gj.Clone();
                        }

                        return Ok(new
                        {
                            name = placeName,
                            displayName,
                            minLat,
                            minLng,
                            maxLat,
                            maxLng,
                            geojson = geoJson
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Lỗi tìm kiếm OpenStreetMap: {ex.Message}" });
        }

        return NotFound(new { message = $"Không tìm thấy vị trí '{trimmed}' trên OpenStreetMap." });
    }
}