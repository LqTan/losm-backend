using System.Globalization;
using System.Text.Json;
using Areas.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Domain.Entities;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Services;

public sealed class DbGeocodingService : IGeocodingService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private readonly PlacesDbContext _db;
    private readonly IAreaRepository _areaRepo;

    public DbGeocodingService(PlacesDbContext db, IAreaRepository areaRepo)
    {
        _db = db;
        _areaRepo = areaRepo;
    }

    public async Task<GeocodeResult?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var q = query.Trim();

        // 1. Tra cứu trong cơ sở dữ liệu Places với thuật toán chấm điểm Tầng 1 (Token Overlap & Match Scoring)
        var candidates = await _db.Places
            .AsNoTracking()
            .Where(p => !p.IsNotSafe && (EF.Functions.Like(p.Name, $"%{q}%") || (p.Address != null && EF.Functions.Like(p.Address, $"%{q}%"))))
            .Take(30)
            .ToListAsync(cancellationToken);

        Place? match = null;
        if (candidates.Count > 0)
        {
            match = candidates
                .Select(p => new { Place = p, Score = CalculatePlaceScore(p, q) })
                .OrderByDescending(x => x.Score)
                .Select(x => x.Place)
                .FirstOrDefault();
        }

        if (match is not null)
        {
            return new GeocodeResult(
                Name: match.Name,
                DisplayName: match.Address ?? match.Name,
                Latitude: match.Latitude,
                Longitude: match.Longitude,
                Category: match.Category,
                Type: "place",
                Box: null);
        }

        // 2. Tra cứu động trong bảng Areas của hệ thống (nếu query là tên khu vực/quận/huyện)
        try
        {
            var allAreas = await _areaRepo.GetAllAsync(cancellationToken);
            var qLower = q.ToLowerInvariant();
            var matchedArea = allAreas.FirstOrDefault(a =>
                a.Name.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                a.Name.ToLowerInvariant().Contains(qLower) ||
                qLower.Contains(a.Name.ToLowerInvariant()));

            if (matchedArea is not null)
            {
                var centerLat = (matchedArea.BboxMinLat + matchedArea.BboxMaxLat) / 2.0;
                var centerLng = (matchedArea.BboxMinLng + matchedArea.BboxMaxLng) / 2.0;
                return new GeocodeResult(
                    Name: matchedArea.Name,
                    DisplayName: $"{matchedArea.Name}, TP.HCM",
                    Latitude: centerLat,
                    Longitude: centerLng,
                    Category: "Khu vực",
                    Type: "district",
                    Box: null);
            }
        }
        catch
        {
            // Bỏ qua lỗi query Areas nếu có
        }

        // 3. Chỉ dùng dữ liệu đã có trong DB nội bộ (Places & Areas), không gọi dịch vụ ngoài (Nominatim/Photon)
        return null;
    }

    private static async Task<GeocodeResult?> GeocodeViaNominatimAsync(string query, CancellationToken cancellationToken)
    {
        var searchUrl = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(query)}&format=json&limit=1";
        using var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
        req.Headers.Add("User-Agent", "losm-backend/1.0 (contact@losm.duckdns.org)");

        using var resp = await HttpClient.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var first = doc.RootElement[0];
        if (first.TryGetProperty("lat", out var latProp) &&
            first.TryGetProperty("lon", out var lonProp) &&
            double.TryParse(latProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(lonProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            var displayName = first.TryGetProperty("display_name", out var dnProp) ? dnProp.GetString() ?? query : query;
            var name = first.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString())
                ? nameProp.GetString()!
                : query;

            return new GeocodeResult(
                Name: name,
                DisplayName: displayName,
                Latitude: lat,
                Longitude: lon,
                Category: "Địa danh",
                Type: "landmark",
                Box: null
            );
        }

        return null;
    }

    private static int CalculatePlaceScore(Places.Domain.Entities.Place p, string query)
    {
        var q = query.Trim();
        var qLower = q.ToLowerInvariant();
        var pNameLower = p.Name.ToLowerInvariant();
        var pAddressLower = (p.Address ?? string.Empty).ToLowerInvariant();

        int score = 0;

        // 1. Khớp chính xác hoàn toàn tên địa điểm (Exact Match)
        if (pNameLower == qLower)
        {
            score += 1000;
        }
        // 2. Khớp tiền tố (StartsWith)
        else if (pNameLower.StartsWith(qLower))
        {
            score += 500;
        }
        // 3. Khớp chuỗi con (Contains)
        else if (pNameLower.Contains(qLower))
        {
            score += 200;
        }

        // 4. Token Overlap Matching (Độ phủ từ khoá thuần C#, không hardcode)
        var queryWords = qLower.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (queryWords.Length > 0)
        {
            int matchedWordsInName = 0;
            int matchedWordsInAddress = 0;

            foreach (var word in queryWords)
            {
                if (pNameLower.Contains(word))
                {
                    matchedWordsInName++;
                }
                if (pAddressLower.Contains(word))
                {
                    matchedWordsInAddress++;
                }
            }

            // Nếu toàn bộ các từ trong câu query đều xuất hiện trong tên địa điểm
            if (matchedWordsInName == queryWords.Length)
            {
                score += 300;
            }
            else
            {
                // Điểm theo tỷ lệ phần trăm từ khoá khớp
                score += (int)((matchedWordsInName / (double)queryWords.Length) * 150);
            }

            // Nếu các từ trong query xuất hiện trong địa chỉ (ví dụ: query "Nguyễn Huệ" khớp địa chỉ "Nguyễn Huệ")
            if (matchedWordsInAddress == queryWords.Length)
            {
                score += 150;
            }
        }

        // 5. Khớp địa chỉ nguyên cụm
        if (pAddressLower.Contains(qLower))
        {
            score += 100;
        }

        // 6. Tie-breaker: Ưu tiên nhẹ địa điểm có tên ngắn, gọn khi cùng mức điểm
        score -= Math.Min(p.Name.Length, 50);

        return score;
    }
}