using System.Text.Json;
using System.Text.Json.Serialization;
using Areas.Application.Abstractions;
using Areas.Domain.Entities;
using Areas.Infrastructure.Persistence;
using Common.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Areas.Application.Areas.Commands.ImportAreas;

public sealed record AreaImportEntry(
    string Name,
    int Level,
    string? ParentExternalCode,
    string? ExternalCode,
    double BboxMinLat,
    double BboxMinLng,
    double BboxMaxLat,
    double BboxMaxLng);

public sealed record AreaImportResult(
    int Total,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Errors);

public sealed class ImportAreasHandler
{
    private readonly AreasDbContext _db;
    private readonly IAreaRepository _repo;
    private readonly ILogger<ImportAreasHandler> _logger;

    public ImportAreasHandler(
        AreasDbContext db,
        IAreaRepository repo,
        ILogger<ImportAreasHandler> logger)
    {
        _db = db;
        _repo = repo;
        _logger = logger;
    }

    public async Task<AreaImportResult> HandleAsync(
        Stream jsonStream,
        CancellationToken cancellationToken = default)
    {
        List<AreaImportEntry>? entries;
        try
        {
            entries = await JsonSerializer.DeserializeAsync<List<AreaImportEntry>>(
                jsonStream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                },
                cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"Invalid JSON: {ex.Message}");
        }

        if (entries is null || entries.Count == 0)
            throw new ValidationException("JSON file contains no entries.");

        var existing = await _db.Areas
            .AsNoTracking()
            .Where(a => a.ExternalCode != null)
            .ToDictionaryAsync(a => a.ExternalCode!, cancellationToken);

        var byCode = new Dictionary<string, Area>(existing);
        var ordered = entries
            .OrderBy(e => e.Level)
            .ToList();

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var entry in ordered)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                errors.Add($"Entry missing name: {System.Text.Json.JsonSerializer.Serialize(entry)}");
                skipped++;
                continue;
            }

            if (!Enum.IsDefined(typeof(AreaLevel), entry.Level))
            {
                errors.Add($"Invalid level {entry.Level} for '{entry.Name}'");
                skipped++;
                continue;
            }

            if (entry.BboxMinLat >= entry.BboxMaxLat ||
                entry.BboxMinLng >= entry.BboxMaxLng)
            {
                errors.Add($"Invalid bbox for '{entry.Name}'");
                skipped++;
                continue;
            }

            Guid? parentId = null;
            if (!string.IsNullOrWhiteSpace(entry.ParentExternalCode))
            {
                if (byCode.TryGetValue(entry.ParentExternalCode, out var parent))
                {
                    parentId = parent.Id;
                }
                else
                {
                    errors.Add($"Parent '{entry.ParentExternalCode}' not found for '{entry.Name}'");
                    skipped++;
                    continue;
                }
            }

            var code = entry.ExternalCode;
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add($"Entry '{entry.Name}' missing externalCode");
                skipped++;
                continue;
            }

            if (byCode.TryGetValue(code, out var existingArea))
            {
                existingArea.Rename(entry.Name);
                existingArea.SetParent(parentId);
                existingArea.UpdateBbox(
                    entry.BboxMinLat,
                    entry.BboxMinLng,
                    entry.BboxMaxLat,
                    entry.BboxMaxLng);
                _db.Areas.Update(existingArea);
                updated++;
            }
            else
            {
                var newArea = new Area(
                    name: entry.Name,
                    level: (AreaLevel)entry.Level,
                    bboxMinLat: entry.BboxMinLat,
                    bboxMinLng: entry.BboxMinLng,
                    bboxMaxLat: entry.BboxMaxLat,
                    bboxMaxLng: entry.BboxMaxLng,
                    parentId: parentId,
                    externalCode: code,
                    isActive: true);
                await _db.Areas.AddAsync(newArea, cancellationToken);
                byCode[code] = newArea;
                inserted++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Areas batch import: total={Total} inserted={Inserted} updated={Updated} skipped={Skipped}",
            entries.Count, inserted, updated, skipped);

        return new AreaImportResult(
            entries.Count,
            inserted,
            updated,
            skipped,
            errors);
    }
}