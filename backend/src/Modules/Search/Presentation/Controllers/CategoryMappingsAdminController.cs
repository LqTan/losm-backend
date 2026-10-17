using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Search.Application.Abstractions;
using Search.Application.Models;

namespace Search.Presentation.Controllers;

[ApiController]
[Route("api/admin/category-mappings")]
public sealed class CategoryMappingsAdminController : ControllerBase
{
    private readonly ICategoryMappingProvider _provider;

    public CategoryMappingsAdminController(ICategoryMappingProvider provider)
    {
        _provider = provider;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        var config = _provider.GetConfig();
        return Ok(config);
    }

    [HttpPut]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateConfig(
        [FromBody] CategoryMappingConfig config,
        CancellationToken ct)
    {
        var updated = await _provider.UpdateConfigAsync(config, ct);
        return Ok(updated);
    }

    [HttpGet("groups")]
    [AllowAnonymous]
    public IActionResult GetGroups()
    {
        var groups = _provider.GetCategoryGroups();
        return Ok(groups);
    }

    [HttpPost("groups")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpsertGroup(
        [FromBody] UpsertGroupRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.GroupName))
        {
            return BadRequest(new { error = "Tên nhóm (GroupName) là bắt buộc." });
        }

        await _provider.AddOrUpdateGroupAsync(request.GroupName, request.Categories ?? [], ct);
        return Ok(new { message = $"Đã cập nhật nhóm '{request.GroupName}'.", groups = _provider.GetCategoryGroups() });
    }

    [HttpDelete("groups/{name}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteGroup(string name, CancellationToken ct)
    {
        var deleted = await _provider.DeleteGroupAsync(name, ct);
        if (!deleted) return NotFound(new { error = $"Không tìm thấy nhóm '{name}' để xoá." });
        return Ok(new { message = $"Đã xoá nhóm '{name}'.", groups = _provider.GetCategoryGroups() });
    }

    [HttpPost("groups/{name}/categories")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> AddCategoryToGroup(
        string name,
        [FromBody] AddCategoryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return BadRequest(new { error = "Category không được để trống." });
        }

        await _provider.AddCategoryToGroupAsync(name, request.Category, ct);
        return Ok(new { message = $"Đã thêm category '{request.Category}' vào nhóm '{name}'.", groups = _provider.GetCategoryGroups() });
    }

    [HttpDelete("groups/{name}/categories/{category}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RemoveCategoryFromGroup(
        string name,
        string category,
        CancellationToken ct)
    {
        var removed = await _provider.RemoveCategoryFromGroupAsync(name, category, ct);
        if (!removed) return NotFound(new { error = $"Không tìm thấy category '{category}' trong nhóm '{name}'." });
        return Ok(new { message = $"Đã xoá category '{category}' khỏi nhóm '{name}'.", groups = _provider.GetCategoryGroups() });
    }

    [HttpGet("overrides")]
    [AllowAnonymous]
    public IActionResult GetOverrides()
    {
        var overrides = _provider.GetOverrides();
        return Ok(overrides);
    }

    [HttpPost("overrides")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpsertOverride(
        [FromBody] UpsertOverrideRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Keyword) || string.IsNullOrWhiteSpace(request.TargetCategory))
        {
            return BadRequest(new { error = "Keyword và TargetCategory không được để trống." });
        }

        await _provider.AddOrUpdateOverrideAsync(request.Keyword, request.TargetCategory, ct);
        return Ok(new { message = $"Đã cập nhật override '{request.Keyword}' -> '{request.TargetCategory}'." });
    }

    [HttpDelete("overrides/{keyword}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteOverride(string keyword, CancellationToken ct)
    {
        var deleted = await _provider.DeleteOverrideAsync(keyword, ct);
        if (!deleted) return NotFound(new { error = $"Không tìm thấy override '{keyword}' để xoá." });
        return Ok(new { message = $"Đã xoá override '{keyword}'." });
    }

    [HttpPost("reset")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ResetDefaults(CancellationToken ct)
    {
        var res = await _provider.ResetToDefaultsAsync(ct);
        return Ok(new { message = "Đã đặt lại cấu hình 13 nhóm Colab chuẩn mặc định.", config = res });
    }

    [HttpPost("test-match")]
    [AllowAnonymous]
    public IActionResult TestCategoryMatch([FromBody] TestCategoryMatchRequest request)
    {
        var score = _provider.ComputeCategoryMatch(
            request.PlaceCategory,
            request.TargetCategory,
            request.SpecificPreference,
            request.Query);

        return Ok(new
        {
            placeCategory = request.PlaceCategory,
            targetCategory = request.TargetCategory,
            specificPreference = request.SpecificPreference,
            query = request.Query,
            categoryMatchScore = score,
            isMatch = score > 0f
        });
    }
}

public sealed record UpsertGroupRequest(
    string GroupName,
    List<string> Categories
);

public sealed record AddCategoryRequest(
    string Category
);

public sealed record UpsertOverrideRequest(
    string Keyword,
    string TargetCategory
);

public sealed record TestCategoryMatchRequest(
    string? PlaceCategory,
    string? TargetCategory,
    string? SpecificPreference,
    string? Query
);
