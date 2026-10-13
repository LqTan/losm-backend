using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Search.Application.Abstractions;

namespace Search.Presentation.Controllers;

[ApiController]
[Route("api/admin/ranking-model")]
[Authorize(Policy = "AdminOnly")]
public sealed class RankingModelAdminController : ControllerBase
{
    private readonly IOnnxRankingModelService _modelService;

    public RankingModelAdminController(IOnnxRankingModelService modelService)
    {
        _modelService = modelService;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _modelService.GetStatus();
        return Ok(status);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB max
    public async Task<IActionResult> UploadModel(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Vui lòng chọn file .onnx hợp lệ." });
        }

        try
        {
            var status = await _modelService.UploadModelAsync(file, cancellationToken);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    [HttpPost("toggle")]
    public async Task<IActionResult> ToggleModel(
        [FromBody] ToggleModelRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _modelService.SetActiveAsync(request.IsActive, cancellationToken);
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteModel(CancellationToken cancellationToken)
    {
        var status = await _modelService.DeleteModelAsync(cancellationToken);
        return Ok(status);
    }

    [HttpGet("sample-template")]
    [AllowAnonymous]
    public IActionResult GetSampleTemplate()
    {
        const string header = "query_id,rank_candidate,poi_id,name,bm25_norm,category_match,duration_avg_min,duration_max_min,duration_spread_min,distance_avg_km,distance_max_km,duration_avg_rank_norm,duration_spread_rank_norm,participant_count,relevance\n";
        return File(System.Text.Encoding.UTF8.GetBytes(header), "text/csv", "lambdamart_10_features_template.csv");
    }
}

public sealed record ToggleModelRequest(bool IsActive);
