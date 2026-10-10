using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Areas.Application.Abstractions;
using Areas.Domain.Entities;
using Areas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Areas.Infrastructure.Seed;

public sealed class AreasSeeder
{
    private readonly AreasDbContext _db;
    private readonly IAreaRepository _repo;
    private readonly ILogger<AreasSeeder> _logger;

    public AreasSeeder(
        AreasDbContext db,
        IAreaRepository repo,
        ILogger<AreasSeeder> logger)
    {
        _db = db;
        _repo = repo;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.Areas.AnyAsync(cancellationToken))
        {
            _logger.LogInformation(
                "Areas table already populated; skipping seed.");
            return;
        }

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppContext.BaseDirectory;
        var seedPath = Path.Combine(assemblyDir, "Seed", "vn-areas.json");

        if (!File.Exists(seedPath))
        {
            _logger.LogWarning(
                "Seed file not found at {Path}; areas table will be empty.",
                seedPath);
            return;
        }

        var json = await File.ReadAllTextAsync(seedPath, cancellationToken);
        var entries = JsonSerializer.Deserialize<List<SeedEntry>>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });

        if (entries is null || entries.Count == 0)
        {
            _logger.LogWarning("Seed file contained no entries.");
            return;
        }

        var byCode = new Dictionary<string, Area>();
        var ordered = entries
            .OrderBy(e => e.Level)
            .ToList();

        foreach (var entry in ordered)
        {
            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(entry.ParentExternalCode) &&
                byCode.TryGetValue(entry.ParentExternalCode, out var parent))
            {
                parentId = parent.Id;
            }

            var area = new Area(
                name: entry.Name,
                level: entry.Level,
                bboxMinLat: entry.BboxMinLat,
                bboxMinLng: entry.BboxMinLng,
                bboxMaxLat: entry.BboxMaxLat,
                bboxMaxLng: entry.BboxMaxLng,
                parentId: parentId,
                externalCode: entry.ExternalCode,
                isActive: true);

            await _db.Areas.AddAsync(area, cancellationToken);

            if (!string.IsNullOrWhiteSpace(entry.ExternalCode))
            {
                byCode[entry.ExternalCode] = area;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Areas seeded: {Count} rows from {Path}",
            entries.Count, seedPath);
    }

    private sealed class SeedEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("level")]
        public AreaLevel Level { get; set; }

        [JsonPropertyName("parentExternalCode")]
        public string? ParentExternalCode { get; set; }

        [JsonPropertyName("externalCode")]
        public string? ExternalCode { get; set; }

        [JsonPropertyName("bboxMinLat")]
        public double BboxMinLat { get; set; }

        [JsonPropertyName("bboxMinLng")]
        public double BboxMinLng { get; set; }

        [JsonPropertyName("bboxMaxLat")]
        public double BboxMaxLat { get; set; }

        [JsonPropertyName("bboxMaxLng")]
        public double BboxMaxLng { get; set; }
    }
}