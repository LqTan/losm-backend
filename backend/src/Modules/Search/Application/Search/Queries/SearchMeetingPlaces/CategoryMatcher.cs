using Search.Infrastructure.Services;

namespace Search.Application.Search.Queries.SearchMeetingPlaces;

public static class CategoryMatcher
{
    public static Dictionary<string, List<string>> CategoryMap =>
        CategoryMappingProvider.Instance.GetConfig().CategoryGroups;

    public static Dictionary<string, string> SpecificPreferenceOverrides =>
        CategoryMappingProvider.Instance.GetConfig().SpecificOverrides;

    public static float ComputeCategoryMatch(string? placeCategory, string? targetCategory, string? specificPreference = null, string? rawQuery = null)
    {
        return CategoryMappingProvider.Instance.ComputeCategoryMatch(placeCategory, targetCategory, specificPreference, rawQuery);
    }
}
