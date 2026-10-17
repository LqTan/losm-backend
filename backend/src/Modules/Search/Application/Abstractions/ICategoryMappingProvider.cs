using Search.Application.Models;

namespace Search.Application.Abstractions;

public interface ICategoryMappingProvider
{
    CategoryMappingConfig GetConfig();
    Task<CategoryMappingConfig> UpdateConfigAsync(CategoryMappingConfig newConfig, CancellationToken ct = default);
    Task<CategoryMappingConfig> ResetToDefaultsAsync(CancellationToken ct = default);

    // Groups CRUD
    IReadOnlyDictionary<string, List<string>> GetCategoryGroups();
    Task AddOrUpdateGroupAsync(string groupName, IEnumerable<string> categories, CancellationToken ct = default);
    Task<bool> DeleteGroupAsync(string groupName, CancellationToken ct = default);
    Task<bool> AddCategoryToGroupAsync(string groupName, string category, CancellationToken ct = default);
    Task<bool> RemoveCategoryFromGroupAsync(string groupName, string category, CancellationToken ct = default);

    // Overrides CRUD
    IReadOnlyDictionary<string, string> GetOverrides();
    Task AddOrUpdateOverrideAsync(string keyword, string targetCategory, CancellationToken ct = default);
    Task<bool> DeleteOverrideAsync(string keyword, CancellationToken ct = default);

    // Matching
    float ComputeCategoryMatch(string? placeCategory, string? targetCategory, string? specificPreference = null, string? rawQuery = null);
}
