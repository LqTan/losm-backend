using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Places.Domain.Entities;
using Places.Infrastructure.Persistence;
using Search.Application.Abstractions;
using Search.Application.Models;

namespace Search.Infrastructure.Services;

public sealed class CategoryMappingProvider : ICategoryMappingProvider
{
    public static CategoryMappingProvider Instance { get; private set; } = new(null);

    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILogger<CategoryMappingProvider>? _logger;
    private readonly object _lock = new();

    private CategoryMappingConfig _currentConfig;

    public CategoryMappingProvider(
        IServiceScopeFactory? scopeFactory = null,
        ILogger<CategoryMappingProvider>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        _currentConfig = LoadFromDatabaseOrCreateDefaults();
        Instance = this;
    }

    public CategoryMappingConfig GetConfig()
    {
        lock (_lock)
        {
            return CloneConfig(_currentConfig);
        }
    }

    public async Task<CategoryMappingConfig> UpdateConfigAsync(CategoryMappingConfig newConfig, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newConfig);

        var now = DateTime.UtcNow;

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var allMappings = await db.CategoryMappings.ToListAsync(ct);
            db.CategoryMappings.RemoveRange(allMappings);

            var allOverrides = await db.CategoryOverrides.ToListAsync(ct);
            db.CategoryOverrides.RemoveRange(allOverrides);

            foreach (var (group, cats) in newConfig.CategoryGroups)
            {
                var g = group.Trim().ToLowerInvariant();
                foreach (var cat in cats)
                {
                    var c = cat.Trim().ToLowerInvariant();
                    db.CategoryMappings.Add(new CategoryMapping
                    {
                        GroupName = g,
                        Category = c,
                        CreatedAt = now
                    });
                }
            }

            foreach (var (kw, target) in newConfig.SpecificOverrides)
            {
                db.CategoryOverrides.Add(new CategoryOverride
                {
                    Keyword = kw.Trim().ToLowerInvariant(),
                    TargetCategory = target.Trim().ToLowerInvariant(),
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync(ct);
        }

        lock (_lock)
        {
            _currentConfig = CloneConfig(newConfig);
        }

        _logger?.LogInformation("CategoryMappingProvider: updated category mappings in database.");
        return GetConfig();
    }

    public async Task<CategoryMappingConfig> ResetToDefaultsAsync(CancellationToken ct = default)
    {
        var defaults = CreateDefaultConfig();
        var now = DateTime.UtcNow;

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var allMappings = await db.CategoryMappings.ToListAsync(ct);
            db.CategoryMappings.RemoveRange(allMappings);

            var allOverrides = await db.CategoryOverrides.ToListAsync(ct);
            db.CategoryOverrides.RemoveRange(allOverrides);

            foreach (var (group, cats) in defaults.CategoryGroups)
            {
                foreach (var cat in cats)
                {
                    db.CategoryMappings.Add(new CategoryMapping
                    {
                        GroupName = group.ToLowerInvariant(),
                        Category = cat.ToLowerInvariant(),
                        CreatedAt = now
                    });
                }
            }

            foreach (var (kw, target) in defaults.SpecificOverrides)
            {
                db.CategoryOverrides.Add(new CategoryOverride
                {
                    Keyword = kw.ToLowerInvariant(),
                    TargetCategory = target.ToLowerInvariant(),
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync(ct);
        }

        lock (_lock)
        {
            _currentConfig = defaults;
        }

        _logger?.LogInformation("CategoryMappingProvider: reset category mappings to standard 13 Colab groups in database.");
        return GetConfig();
    }

    public IReadOnlyDictionary<string, List<string>> GetCategoryGroups()
    {
        lock (_lock)
        {
            return new Dictionary<string, List<string>>(_currentConfig.CategoryGroups, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task AddOrUpdateGroupAsync(string groupName, IEnumerable<string> categories, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;

        var cleanedName = groupName.Trim().ToLowerInvariant();
        var catList = categories
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var existing = await db.CategoryMappings
                .Where(m => m.GroupName == cleanedName)
                .ToListAsync(ct);
            db.CategoryMappings.RemoveRange(existing);

            var now = DateTime.UtcNow;
            foreach (var cat in catList)
            {
                db.CategoryMappings.Add(new CategoryMapping
                {
                    GroupName = cleanedName,
                    Category = cat,
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync(ct);
        }

        lock (_lock)
        {
            _currentConfig.CategoryGroups[cleanedName] = catList;
        }

        _logger?.LogInformation("CategoryMappingProvider: updated group '{GroupName}' with {Count} categories in database.", cleanedName, catList.Count);
    }

    public async Task<bool> DeleteGroupAsync(string groupName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return false;

        var cleanedName = groupName.Trim().ToLowerInvariant();
        bool removed = false;

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var existing = await db.CategoryMappings
                .Where(m => m.GroupName == cleanedName)
                .ToListAsync(ct);

            if (existing.Count > 0)
            {
                db.CategoryMappings.RemoveRange(existing);
                await db.SaveChangesAsync(ct);
                removed = true;
            }
        }

        lock (_lock)
        {
            removed = _currentConfig.CategoryGroups.Remove(cleanedName) || removed;
        }

        if (removed)
        {
            _logger?.LogInformation("CategoryMappingProvider: deleted group '{GroupName}' from database.", cleanedName);
        }

        return removed;
    }

    public async Task<bool> AddCategoryToGroupAsync(string groupName, string category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(category)) return false;

        var gName = groupName.Trim().ToLowerInvariant();
        var cat = category.Trim().ToLowerInvariant();

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var exists = await db.CategoryMappings
                .AnyAsync(m => m.GroupName == gName && m.Category == cat, ct);

            if (!exists)
            {
                db.CategoryMappings.Add(new CategoryMapping
                {
                    GroupName = gName,
                    Category = cat,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync(ct);
            }
        }

        lock (_lock)
        {
            if (!_currentConfig.CategoryGroups.TryGetValue(gName, out var list))
            {
                list = [];
                _currentConfig.CategoryGroups[gName] = list;
            }

            if (!list.Contains(cat, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(cat);
            }
        }

        return true;
    }

    public async Task<bool> RemoveCategoryFromGroupAsync(string groupName, string category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(category)) return false;

        var gName = groupName.Trim().ToLowerInvariant();
        var cat = category.Trim().ToLowerInvariant();
        bool removed = false;

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var mapping = await db.CategoryMappings
                .FirstOrDefaultAsync(m => m.GroupName == gName && m.Category == cat, ct);

            if (mapping is not null)
            {
                db.CategoryMappings.Remove(mapping);
                await db.SaveChangesAsync(ct);
                removed = true;
            }
        }

        lock (_lock)
        {
            if (_currentConfig.CategoryGroups.TryGetValue(gName, out var list))
            {
                removed = list.RemoveAll(c => c.Equals(cat, StringComparison.OrdinalIgnoreCase)) > 0 || removed;
            }
        }

        return removed;
    }

    public IReadOnlyDictionary<string, string> GetOverrides()
    {
        lock (_lock)
        {
            return new Dictionary<string, string>(_currentConfig.SpecificOverrides, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task AddOrUpdateOverrideAsync(string keyword, string targetCategory, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(targetCategory)) return;

        var kw = keyword.Trim().ToLowerInvariant();
        var target = targetCategory.Trim().ToLowerInvariant();

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var existing = await db.CategoryOverrides.FirstOrDefaultAsync(o => o.Keyword == kw, ct);
            if (existing is not null)
            {
                existing.TargetCategory = target;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                db.CategoryOverrides.Add(new CategoryOverride
                {
                    Keyword = kw,
                    TargetCategory = target,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync(ct);
        }

        lock (_lock)
        {
            _currentConfig.SpecificOverrides[kw] = target;
        }
    }

    public async Task<bool> DeleteOverrideAsync(string keyword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;

        var kw = keyword.Trim().ToLowerInvariant();
        bool removed = false;

        if (_scopeFactory is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var existing = await db.CategoryOverrides.FirstOrDefaultAsync(o => o.Keyword == kw, ct);
            if (existing is not null)
            {
                db.CategoryOverrides.Remove(existing);
                await db.SaveChangesAsync(ct);
                removed = true;
            }
        }

        lock (_lock)
        {
            removed = _currentConfig.SpecificOverrides.Remove(kw) || removed;
        }

        return removed;
    }

    public float ComputeCategoryMatch(
        string? placeCategory,
        string? targetCategory,
        string? specificPreference = null,
        string? rawQuery = null)
    {
        if (string.IsNullOrWhiteSpace(placeCategory))
            return 0f;

        var catRaw = placeCategory.Trim().ToLowerInvariant();

        CategoryMappingConfig cfg;
        lock (_lock)
        {
            cfg = _currentConfig;
        }

        // 1. Kiểm tra specific preference (ramen, sushi, dim sum, pizza, món ý, món pháp...)
        var pref = (specificPreference ?? string.Empty).Trim().ToLowerInvariant();
        var q = (rawQuery ?? string.Empty).Trim().ToLowerInvariant();

        foreach (var (specKeyword, targetCat) in cfg.SpecificOverrides)
        {
            if ((!string.IsNullOrEmpty(pref) && pref.Contains(specKeyword, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(q) && q.Contains(specKeyword, StringComparison.OrdinalIgnoreCase)))
            {
                return catRaw.Equals(targetCat, StringComparison.OrdinalIgnoreCase) || catRaw.Contains(targetCat)
                    ? 1f : 0f;
            }
        }

        // 2. Kiểm tra theo targetCategory từ MiniMax (cafe, viet, japanese, park...)
        var target = (targetCategory ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(target) && !target.Equals("other", StringComparison.OrdinalIgnoreCase))
        {
            if (cfg.CategoryGroups.TryGetValue(target, out var groupCats))
            {
                return groupCats.Any(c => catRaw.Equals(c, StringComparison.OrdinalIgnoreCase) || catRaw.Contains(c, StringComparison.OrdinalIgnoreCase))
                    ? 1f : 0f;
            }
        }

        // 3. Fallback nếu không có targetCategory rõ ràng: dò qua các nhóm xem có category nào khớp với placeCategory
        if (string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(q))
        {
            foreach (var (gName, cats) in cfg.CategoryGroups)
            {
                if (q.Contains(gName, StringComparison.OrdinalIgnoreCase))
                {
                    if (cats.Any(c => catRaw.Equals(c, StringComparison.OrdinalIgnoreCase) || catRaw.Contains(c, StringComparison.OrdinalIgnoreCase)))
                    {
                        return 1f;
                    }
                }
            }
        }

        return 0f;
    }

    private void EnsureTablesExist(PlacesDbContext db)
    {
        try
        {
            db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.CategoryMappings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CategoryMappings] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [GroupName] nvarchar(100) NOT NULL,
        [Category] nvarchar(150) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CategoryMappings] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_CategoryMappings_GroupName] ON [dbo].[CategoryMappings] ([GroupName] ASC);
    CREATE NONCLUSTERED INDEX [IX_CategoryMappings_Category] ON [dbo].[CategoryMappings] ([Category] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [IX_CategoryMappings_GroupName_Category] ON [dbo].[CategoryMappings] ([GroupName] ASC, [Category] ASC);
END;

IF OBJECT_ID(N'dbo.CategoryOverrides', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CategoryOverrides] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Keyword] nvarchar(150) NOT NULL,
        [TargetCategory] nvarchar(150) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_CategoryOverrides] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [IX_CategoryOverrides_Keyword] ON [dbo].[CategoryOverrides] ([Keyword] ASC);
END;
");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "EnsureTablesExist encountered an error (tables may already exist).");
        }
    }

    private CategoryMappingConfig LoadFromDatabaseOrCreateDefaults()
    {
        if (_scopeFactory is null)
        {
            return CreateDefaultConfig();
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PlacesDbContext>();

            EnsureTablesExist(db);

            var dbMappings = db.CategoryMappings.AsNoTracking().ToList();
            var dbOverrides = db.CategoryOverrides.AsNoTracking().ToList();

            if (dbMappings.Count == 0)
            {
                var def = CreateDefaultConfig();
                var now = DateTime.UtcNow;

                foreach (var (group, cats) in def.CategoryGroups)
                {
                    foreach (var cat in cats)
                    {
                        db.CategoryMappings.Add(new CategoryMapping
                        {
                            GroupName = group.ToLowerInvariant(),
                            Category = cat.ToLowerInvariant(),
                            CreatedAt = now
                        });
                    }
                }

                foreach (var (kw, target) in def.SpecificOverrides)
                {
                    db.CategoryOverrides.Add(new CategoryOverride
                    {
                        Keyword = kw.ToLowerInvariant(),
                        TargetCategory = target.ToLowerInvariant(),
                        CreatedAt = now
                    });
                }

                db.SaveChanges();
                _logger?.LogInformation("CategoryMappingProvider: Seeded default {Count} category mappings and {OverrideCount} overrides into SQL Server database.",
                    def.CategoryGroups.Values.Sum(v => v.Count), def.SpecificOverrides.Count);
                return def;
            }

            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in dbMappings)
            {
                var g = m.GroupName.Trim().ToLowerInvariant();
                var c = m.Category.Trim().ToLowerInvariant();
                if (!groups.TryGetValue(g, out var list))
                {
                    list = [];
                    groups[g] = list;
                }
                if (!list.Contains(c, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(c);
                }
            }

            var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in dbOverrides)
            {
                overrides[o.Keyword.Trim().ToLowerInvariant()] = o.TargetCategory.Trim().ToLowerInvariant();
            }

            _logger?.LogInformation("CategoryMappingProvider: Loaded {GroupCount} groups ({MappingCount} mappings) from SQL Server database.",
                groups.Count, dbMappings.Count);

            return new CategoryMappingConfig
            {
                CategoryGroups = groups,
                SpecificOverrides = overrides
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load category mappings from database. Falling back to defaults.");
            return CreateDefaultConfig();
        }
    }

    private static CategoryMappingConfig CloneConfig(CategoryMappingConfig src)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in src.CategoryGroups)
        {
            groups[k] = new List<string>(v);
        }

        var overrides = new Dictionary<string, string>(src.SpecificOverrides, StringComparer.OrdinalIgnoreCase);
        return new CategoryMappingConfig
        {
            CategoryGroups = groups,
            SpecificOverrides = overrides
        };
    }

    public static CategoryMappingConfig CreateDefaultConfig()
    {
        return new CategoryMappingConfig
        {
            CategoryGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["cafe"] = ["cafe", "coffee_shop", "tea_house", "tea_room"],
                ["viet"] = ["vietnamese_restaurant"],
                ["japanese"] = ["japanese_restaurant", "sushi_restaurant", "ramen_restaurant"],
                ["korean"] = ["korean_restaurant"],
                ["thai"] = ["thai_restaurant"],
                ["seafood"] = ["seafood_restaurant"],
                ["western"] = ["italian_restaurant", "french_restaurant", "european_restaurant", "pizza_restaurant"],
                ["vegetarian"] = ["vegetarian_restaurant", "vegan_restaurant"],
                ["chinese"] = ["chinese_restaurant", "dim_sum_restaurant"],
                ["bbq_hotpot"] = ["barbecue_restaurant", "hotpot_restaurant"],
                ["bookstore"] = ["bookstore", "book_store", "book_shop", "comic_books_store"],
                ["library"] = ["library", "public_library"],
                ["park"] = ["park", "urban_park", "public_park", "botanical_garden"]
            },
            SpecificOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ramen"] = "ramen_restaurant",
                ["sushi"] = "sushi_restaurant",
                ["dim sum"] = "dim_sum_restaurant",
                ["pizza"] = "pizza_restaurant",
                ["món ý"] = "italian_restaurant",
                ["mon y"] = "italian_restaurant",
                ["món pháp"] = "french_restaurant",
                ["mon phap"] = "french_restaurant"
            }
        };
    }
}
