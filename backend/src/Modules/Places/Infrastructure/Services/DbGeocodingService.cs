using Areas.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Domain.Entities;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Services;

public sealed class DbGeocodingService : IGeocodingService
{
    private readonly PlacesDbContext _db;
    private readonly IAreaRepository _areaRepo;

    public DbGeocodingService(PlacesDbContext db, IAreaRepository areaRepo)
    {
        _db = db;
        _areaRepo = areaRepo;
    }

    public Task<GeocodeResult?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default) =>
        GeocodeAsync(query, null, cancellationToken);

    public async Task<GeocodeResult?> GeocodeAsync(
        string placeName,
        string? address,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(placeName)) return null;

        var nameTarget = placeName.Trim();
        var addrTarget = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

        // 1. Tìm ứng viên trong cơ sở dữ liệu Places theo Name và Address
        var queryable = _db.Places
            .AsNoTracking()
            .Where(p => !p.IsNotSafe);

        if (!string.IsNullOrEmpty(addrTarget))
        {
            queryable = queryable.Where(p =>
                EF.Functions.Like(p.Name, $"%{nameTarget}%") ||
                (p.Address != null && EF.Functions.Like(p.Address, $"%{addrTarget}%")));
        }
        else
        {
            queryable = queryable.Where(p =>
                EF.Functions.Like(p.Name, $"%{nameTarget}%") ||
                (p.Address != null && EF.Functions.Like(p.Address, $"%{nameTarget}%")));
        }

        var candidates = await queryable
            .Take(50)
            .ToListAsync(cancellationToken);

        Place? match = null;
        if (candidates.Count > 0)
        {
            match = candidates
                .Select(p => new { Place = p, Score = CalculateScore(p, nameTarget, addrTarget) })
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

        // 2. Tra cứu trong bảng Areas (CHỈ KHI không tìm thấy Place và placeName trùng khớp với tên Area trong DB)
        var allAreas = await _areaRepo.GetAllAsync(cancellationToken);
        var matchedArea = allAreas.FirstOrDefault(a => 
            a.Name.Equals(nameTarget, StringComparison.OrdinalIgnoreCase));

        if (matchedArea is not null)
        {
            var centerLat = (matchedArea.BboxMinLat + matchedArea.BboxMaxLat) / 2.0;
            var centerLng = (matchedArea.BboxMinLng + matchedArea.BboxMaxLng) / 2.0;
            return new GeocodeResult(
                Name: matchedArea.Name,
                DisplayName: matchedArea.Name,
                Latitude: centerLat,
                Longitude: centerLng,
                Category: "Khu vực",
                Type: "district",
                Box: null);
        }

        return null;
    }

    private static int CalculateScore(Place p, string nameTarget, string? addrTarget)
    {
        var pName = p.Name.Trim().ToLowerInvariant();
        var pAddr = (p.Address ?? string.Empty).Trim().ToLowerInvariant();
        var targetNameLower = nameTarget.ToLowerInvariant();

        int score = 0;

        // 1. So khớp tên địa điểm
        if (pName == targetNameLower)
        {
            score += 1000;
        }
        else if (pName.StartsWith(targetNameLower))
        {
            score += 600;
        }
        else if (pName.Contains(targetNameLower))
        {
            score += 300;
        }
        else if (targetNameLower.Contains(pName))
        {
            score += 200;
        }

        // 2. So khớp địa chỉ (nếu có addrTarget)
        if (!string.IsNullOrEmpty(addrTarget))
        {
            var targetAddrLower = addrTarget.ToLowerInvariant();
            if (pAddr.Contains(targetAddrLower))
            {
                score += 500;
            }
        }

        // 3. Ưu tiên địa điểm có tên ngắn gọn hơn khi cùng mức độ khớp
        score -= Math.Min(p.Name.Length, 50);

        return score;
    }
}