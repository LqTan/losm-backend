namespace Search.Application.Models;

public sealed class CategoryMappingConfig
{
    public Dictionary<string, List<string>> CategoryGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SpecificOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
