using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Services;

public sealed class DbGeocodingService : IGeocodingService
{
    private readonly PlacesDbContext _db;

    public DbGeocodingService(PlacesDbContext db)
    {
        _db = db;
    }

    public async Task<GeocodeResult?> GeocodeAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var q = query.Trim();
        var match = await _db.Places
            .AsNoTracking()
            .Where(p => EF.Functions.Like(p.Name, $"%{q}%"))
            .OrderBy(p => p.Name.Length)
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null && q.Contains("ubnd", StringComparison.OrdinalIgnoreCase))
        {
            var alt = q.Replace("ubnd", "ủy ban nhân dân", StringComparison.OrdinalIgnoreCase);
            match = await _db.Places
                .AsNoTracking()
                .Where(p => EF.Functions.Like(p.Name, $"%{alt}%"))
                .OrderBy(p => p.Name.Length)
                .FirstOrDefaultAsync(cancellationToken);
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

        // Fallback for well-known locations / districts in HCMC
        var lower = q.ToLowerInvariant();
        if (lower.Contains("bến thành"))
            return new GeocodeResult("Chợ Bến Thành", "Chợ Bến Thành, Quận 1", 10.7725, 106.6980, "Chợ", "landmark", null);
        if (lower.Contains("ubnd") && (lower.Contains("quận 1") || lower.Contains("q1") || lower.Contains("q.1")))
            return new GeocodeResult("UBND Quận 1", "47 Lê Duẩn, Bến Nghé, Quận 1", 10.7782, 106.7011, "Cơ quan", "landmark", null);
        if (lower.Contains("đức bà"))
            return new GeocodeResult("Nhà thờ Đức Bà", "01 Công xã Paris, Bến Nghé, Quận 1", 10.7797, 106.6990, "Nhà thờ", "landmark", null);
        if (lower.Contains("độc lập") || lower.Contains("dinh"))
            return new GeocodeResult("Dinh Độc Lập", "135 Nam Kỳ Khởi Nghĩa, Bến Thành, Quận 1", 10.7770, 106.6953, "Di tích", "landmark", null);
        if (lower.Contains("nguyễn huệ"))
            return new GeocodeResult("Phố đi bộ Nguyễn Huệ", "Nguyễn Huệ, Bến Nghé, Quận 1", 10.7745, 106.7037, "Phố đi bộ", "landmark", null);
        if (lower.Contains("con rùa"))
            return new GeocodeResult("Hồ Con Rùa", "Công trường Quốc Tế, Quận 3", 10.7828, 106.6958, "Địa danh", "landmark", null);
        if (lower.Contains("landmark 81") || lower.Contains("landmark"))
            return new GeocodeResult("Landmark 81", "720A Điện Biên Phủ, Bình Thạnh", 10.7950, 106.7219, "Trung tâm thương mại", "landmark", null);
        if (lower.Contains("hàng xanh"))
            return new GeocodeResult("Ngã tư Hàng Xanh", "Bình Thạnh, TP.HCM", 10.8016, 106.7114, "Giao lộ", "landmark", null);
        if (lower.Contains("quận 1") || lower.Contains("q1") || lower.Contains("q.1"))
            return new GeocodeResult("Quận 1", "Trung tâm Quận 1, TP.HCM", 10.7769, 106.7009, "Khu vực", "district", null);
        if (lower.Contains("quận 3") || lower.Contains("q3") || lower.Contains("q.3"))
            return new GeocodeResult("Quận 3", "Quận 3, TP.HCM", 10.7828, 106.6958, "Khu vực", "district", null);
        if (lower.Contains("quận 5") || lower.Contains("q5") || lower.Contains("q.5") || lower.Contains("chợ lớn"))
            return new GeocodeResult("Quận 5", "Quận 5, TP.HCM", 10.7544, 106.6633, "Khu vực", "district", null);
        if (lower.Contains("bình thạnh"))
            return new GeocodeResult("Bình Thạnh", "Bình Thạnh, TP.HCM", 10.8016, 106.7114, "Khu vực", "district", null);
        if (lower.Contains("phú nhuận"))
            return new GeocodeResult("Phú Nhuận", "Phú Nhuận, TP.HCM", 10.7977, 106.6872, "Khu vực", "district", null);
        if (lower.Contains("quận 10") || lower.Contains("q10") || lower.Contains("q.10"))
            return new GeocodeResult("Quận 10", "Quận 10, TP.HCM", 10.7711, 106.6698, "Khu vực", "district", null);
        if (lower.Contains("tân bình"))
            return new GeocodeResult("Tân Bình", "Tân Bình, TP.HCM", 10.7941, 106.6612, "Khu vực", "district", null);
        if (lower.Contains("gò vấp"))
            return new GeocodeResult("Gò Vấp", "Gò Vấp, TP.HCM", 10.8384, 106.6651, "Khu vực", "district", null);
        if (lower.Contains("quận 7") || lower.Contains("q7") || lower.Contains("q.7"))
            return new GeocodeResult("Quận 7", "Quận 7, TP.HCM", 10.7292, 106.7126, "Khu vực", "district", null);
        if (lower.Contains("thủ đức") || lower.Contains("thảo điền"))
            return new GeocodeResult("Thủ Đức", "TP. Thủ Đức, TP.HCM", 10.8038, 106.7369, "Khu vực", "district", null);

        return null;
    }
}