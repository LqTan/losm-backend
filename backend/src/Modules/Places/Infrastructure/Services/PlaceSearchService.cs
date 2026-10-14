using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Services;

public sealed class PlaceSpatialSearchService : IPlaceSpatialSearchService
{
    private readonly PlacesDbContext _db;

    private static readonly (string[] Triggers, string[] Categories, string[] NameKeywords)[] IntentMap =
    [
        (
            Triggers: ["cà phê", "ca phe", "quán cà phê", "quan ca phe", "cafe", "càfe", "coffee", "bạc xỉu", "caphe", "cappuccino", "latte", "espresso"],
            Categories: ["cafe", "coffee_shop", "coffee", "tea_house"],
            NameKeywords: ["cafe", "coffee", "cà phê"]
        ),
        (
            Triggers: ["trà sữa", "tra sua", "milk tea", "boba", "bubble tea", "tiệm trà", "quán trà"],
            Categories: ["bubble_tea_shop", "tea_house", "cafe"],
            NameKeywords: ["tea", "trà", "trà sữa"]
        ),
        (
            Triggers: ["nhà hàng", "nha hang", "quán ăn", "quan an", "ăn uống", "an uong", "ẩm thực", "restaurant", "quán cơm", "bún", "phở", "lẩu", "nướng", "bbq", "bistro", "quán lẩu", "quán nướng"],
            Categories: ["restaurant", "fast_food_restaurant", "asian_restaurant", "bar_and_grill_restaurant"],
            NameKeywords: ["restaurant", "nhà hàng", "quán ăn", "quán cơm", "bistro", "lẩu", "nướng", "phở", "bún"]
        ),
        (
            Triggers: ["quán bar", "quán bia", "bar", "pub", "bia", "beer", "club", "lounge"],
            Categories: ["bar", "pub", "bar_and_grill_restaurant"],
            NameKeywords: ["bar", "pub", "lounge", "beer", "bia"]
        ),
        (
            Triggers: ["tiệm bánh", "tiem banh", "bánh ngọt", "bakery", "bánh mì", "banh mi", "cake"],
            Categories: ["bakery", "bagel_shop"],
            NameKeywords: ["bakery", "bánh", "cake"]
        )
    ];

    private static readonly string[] Fillers =
    [
        "tìm kiếm", "tìm giúp tôi", "tìm giúp mình", "tìm giúp", "tìm cho tôi", "tìm cho mình", "tìm quán", "tìm chỗ", "tìm địa điểm", "tìm",
        "gợi ý cho tôi", "gợi ý", "chỉ cho tôi", "cho tôi", "cho mình", "hãy tìm", "hãy gợi ý",
        "tôi muốn", "mình muốn", "chúng tôi muốn", "nhóm muốn", "muốn", "cần tìm", "cần",
        "có không gian", "không gian", "yên tĩnh", "làm việc", "học bài", "view đẹp", "máy lạnh", "máy tính",
        "wifi", "ổ điện", "rộng rãi", "họp nhóm", "hẹn hò", "bạn bè", "gặp nhau", "nói chuyện",
        "ở quận 1", "ở quận 2", "ở quận 3", "ở quận 4", "ở quận 5", "ở quận 7", "ở quận 10",
        "quận 1", "quận 2", "quận 3", "quận 4", "quận 5", "quận 7", "quận 10", "q1", "q2", "q3", "q5", "q7",
        "quận bình thạnh", "quận tân bình", "quận phú nhuận", "quận gò vấp",
        "khu vực", "ở khu vực", "ở đâu", "gần đây", "quanh đây", "gần nhất", "gần tôi", "ở trung tâm", "trung tâm",
        "thích hợp", "phù hợp", "công bằng", "ở giữa", "điểm hẹn", "hẹn nhau"
    ];

    public PlaceSpatialSearchService(PlacesDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlaceSearchHit>> SearchAsync(
        string query,
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 20;
        if (radiusKm <= 0) radiusKm = 5;

        var latDelta = radiusKm / 111.0;
        var cosLat = Math.Cos(latitude * Math.PI / 180.0);
        var lngDelta = Math.Max(0.001, radiusKm / (111.0 * cosLat));

        var clean = (query ?? "").Trim();
        var lower = clean.ToLowerInvariant();

        // 1. Phân giải danh mục (Intent mapping)
        List<string>? matchedCategories = null;
        List<string>? matchedNameKeywords = null;

        foreach (var intent in IntentMap)
        {
            if (intent.Triggers.Any(t => lower.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                matchedCategories = intent.Categories.ToList();
                matchedNameKeywords = intent.NameKeywords.ToList();
                break;
            }
        }

        // 2. Bóc tách từ phụ (Fillers) & từ khóa riêng (Brand/Location cụ thể)
        var residual = lower;
        foreach (var filler in Fillers)
        {
            residual = residual.Replace(filler, " ", StringComparison.OrdinalIgnoreCase);
        }
        if (matchedCategories is not null)
        {
            foreach (var intent in IntentMap)
            {
                foreach (var t in intent.Triggers)
                {
                    residual = residual.Replace(t, " ", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        residual = string.Join(" ", residual.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        // Bóc tách cụm từ vị trí mốc (ví dụ: "gần chợ bến thành", "gần bệnh viện từ dũ") khỏi từ khóa tên thương hiệu
        residual = System.Text.RegularExpressions.Regex.Replace(
            residual,
            @"\b(gần|quanh|khu vực|tại|ở|phía|hướng|cạnh|đối diện)\s+.*$",
            " ",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        residual = string.Join(" ", residual.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        // 3. Xây dựng truy vấn cơ sở theo vùng địa lý (chỉ lấy địa điểm an toàn, hợp lệ)
        var baseQuery = _db.Places.AsNoTracking().Where(p =>
            !p.IsNotSafe &&
            p.Latitude >= latitude - latDelta &&
            p.Latitude <= latitude + latDelta &&
            p.Longitude >= longitude - lngDelta &&
            p.Longitude <= longitude + lngDelta);

        string? cat1 = matchedCategories?.ElementAtOrDefault(0);
        string? cat2 = matchedCategories?.ElementAtOrDefault(1);
        string? cat3 = matchedCategories?.ElementAtOrDefault(2);
        string? cat4 = matchedCategories?.ElementAtOrDefault(3);

        string? kw1 = matchedNameKeywords?.ElementAtOrDefault(0);
        string? kw2 = matchedNameKeywords?.ElementAtOrDefault(1);
        string? kw3 = matchedNameKeywords?.ElementAtOrDefault(2);

        if (matchedCategories is not null && matchedCategories.Count > 0)
        {
            // Lọc theo Category hoặc từ khóa tên quán
            baseQuery = baseQuery.Where(p =>
                (p.Category != null && (p.Category == cat1 || p.Category == cat2 || p.Category == cat3 || p.Category == cat4)) ||
                (kw1 != null && EF.Functions.Like(p.Name, $"%{kw1}%")) ||
                (kw2 != null && EF.Functions.Like(p.Name, $"%{kw2}%")) ||
                (kw3 != null && EF.Functions.Like(p.Name, $"%{kw3}%")));

            // Nếu người dùng có nhập tên thương hiệu riêng (ví dụ: "Highlands", "Trung Nguyên")
            if (!string.IsNullOrWhiteSpace(residual) && residual.Length >= 2)
            {
                var brandPattern = $"%{residual}%";
                baseQuery = baseQuery.Where(p =>
                    EF.Functions.Like(p.Name, brandPattern) ||
                    (p.Address != null && EF.Functions.Like(p.Address, brandPattern)));
            }
        }
        else if (!string.IsNullOrWhiteSpace(residual) && residual.Length >= 2)
        {
            var brandPattern = $"%{residual}%";
            baseQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Name, brandPattern) ||
                (p.Address != null && EF.Functions.Like(p.Address, brandPattern)) ||
                (p.Category != null && EF.Functions.Like(p.Category, brandPattern)));
        }
        else if (!string.IsNullOrWhiteSpace(clean))
        {
            var pattern = $"%{clean}%";
            baseQuery = baseQuery.Where(p =>
                EF.Functions.Like(p.Name, pattern) ||
                (p.Address != null && EF.Functions.Like(p.Address, pattern)) ||
                (p.Category != null && EF.Functions.Like(p.Category, pattern)));
        }

        var candidates = await baseQuery
            .Take(Math.Min(limit * 10, 1000))
            .ToListAsync(cancellationToken);

        // Fallback 1: Nếu có nhập thương hiệu riêng nhưng không tìm thấy quán mang tên đó, trả về các quán cùng danh mục
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(residual) && matchedCategories is not null)
        {
            var categoryFallbackQuery = _db.Places.AsNoTracking().Where(p =>
                !p.IsNotSafe &&
                p.Latitude >= latitude - latDelta &&
                p.Latitude <= latitude + latDelta &&
                p.Longitude >= longitude - lngDelta &&
                p.Longitude <= longitude + lngDelta &&
                p.Category != null && (p.Category == cat1 || p.Category == cat2 || p.Category == cat3 || p.Category == cat4));

            candidates = await categoryFallbackQuery
                .Take(Math.Min(limit * 10, 1000))
                .ToListAsync(cancellationToken);
        }

        // Fallback 2: Nếu vẫn trống, lấy các địa điểm nổi bật chung trong bán kính
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(clean))
        {
            var radiusFallbackQuery = _db.Places.AsNoTracking().Where(p =>
                !p.IsNotSafe &&
                p.Latitude >= latitude - latDelta &&
                p.Latitude <= latitude + latDelta &&
                p.Longitude >= longitude - lngDelta &&
                p.Longitude <= longitude + lngDelta &&
                p.Category != null && (p.Category == "cafe" || p.Category == "coffee_shop" || p.Category == "restaurant"));

            candidates = await radiusFallbackQuery
                .Take(Math.Min(limit * 10, 1000))
                .ToListAsync(cancellationToken);
        }

        var radiusMeters = radiusKm * 1000.0;
        var hits = candidates
            .Select(p =>
            {
                var dLat = (p.Latitude - latitude) * 111000.0;
                var dLng = (p.Longitude - longitude) * 111000.0 * cosLat;
                var distance = Math.Sqrt(dLat * dLat + dLng * dLng);
                return new PlaceSearchHit(
                    p.Id, p.Name, p.Address, p.Category, p.OpeningHours,
                    p.Latitude, p.Longitude, distance);
            })
            .Where(h => h.DistanceMeters <= radiusMeters)
            .OrderBy(h => h.DistanceMeters)
            .Take(limit)
            .ToList();

        return hits;
    }
}