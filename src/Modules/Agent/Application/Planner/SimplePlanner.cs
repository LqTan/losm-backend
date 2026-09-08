using Agent.Application.Abstractions;
using Agent.Domain;

namespace Agent.Application.Planner;

public sealed class SimplePlanner : IPlanner
{
    public AgentPlan CreatePlan(string query, bool hasUserLocation)
    {
        var normalizedQuery = (query ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return new AgentPlan
            {
                OriginalQuery = string.Empty,
                SearchQuery = string.Empty,
                NeedsClarification = true,
                ClarificationQuestion =
                    "Bạn muốn tìm loại địa điểm nào? Ví dụ: quán cà phê, nhà hàng hoặc thư viện."
            };
        }

        var requiresLocation =
            normalizedQuery.Contains("gần tôi") ||
            normalizedQuery.Contains("quanh đây") ||
            normalizedQuery.Contains("near me");

        var placeType = DetectPlaceType(normalizedQuery);
        var preferences = DetectPreferences(normalizedQuery);
        var searchQuery = BuildSearchQuery(normalizedQuery, placeType);

        if (requiresLocation && !hasUserLocation)
        {
            return new AgentPlan
            {
                OriginalQuery = normalizedQuery,
                SearchQuery = searchQuery,
                PlaceType = placeType,
                Preferences = preferences,
                RadiusKm = 5,
                RequiresLocation = true,
                ShouldLoadReviews = true,
                ShouldUseSemanticRerank = true,
                NeedsClarification = true,
                ClarificationQuestion =
                    "Bạn hãy bật vị trí hoặc nhập khu vực muốn tìm để mình gợi ý địa điểm phù hợp."
            };
        }

        return new AgentPlan
        {
            OriginalQuery = normalizedQuery,
            SearchQuery = searchQuery,
            PlaceType = placeType,
            Preferences = preferences,
            RadiusKm = 5,
            RequiresLocation = requiresLocation,
            ShouldLoadReviews = true,
            ShouldUseSemanticRerank = true,
            NeedsClarification = false
        };
    }

    private static string BuildSearchQuery(string normalizedQuery, string? placeType)
    {
        if (!string.IsNullOrWhiteSpace(placeType))
        {
            return placeType;
        }

        return normalizedQuery;
    }

    private static string? DetectPlaceType(string query)
    {
        if (query.Contains("cà phê") || query.Contains("cafe") || query.Contains("coffee"))
        {
            return "cafe";
        }

        if (query.Contains("nhà hàng") || query.Contains("quán ăn") || query.Contains("restaurant"))
        {
            return "restaurant";
        }

        if (query.Contains("thư viện") || query.Contains("library"))
        {
            return "library";
        }

        if (query.Contains("khách sạn") || query.Contains("hotel"))
        {
            return "hotel";
        }

        if (query.Contains("bar") || query.Contains("pub"))
        {
            return "bar";
        }

        return null;
    }

    private static List<string> DetectPreferences(string query)
    {
        var preferences = new List<string>();

        if (query.Contains("yên tĩnh"))
        {
            preferences.Add("quiet");
        }

        if (query.Contains("view đẹp") || query.Contains("cảnh đẹp"))
        {
            preferences.Add("nice_view");
        }

        if (query.Contains("học bài") || query.Contains("làm việc"))
        {
            preferences.Add("study_friendly");
        }

        if (query.Contains("gia đình"))
        {
            preferences.Add("family_friendly");
        }

        return preferences;
    }
}