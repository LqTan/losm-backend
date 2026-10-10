using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Domain.Entities;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Repositories;

public sealed class PlaceRepository : IPlaceRepository
{
    private readonly PlacesDbContext _dbContext;

    public PlaceRepository(PlacesDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<Place?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<Place>> SearchByNameAsync(
        string name,
        int limit = 5,
        CancellationToken cancellationToken = default
    )
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        var pattern = $"%{trimmed}%";

        var containsMatches = await _dbContext.Places
            .AsNoTracking()
            .Where(x =>
                x.Name != null &&
                EF.Functions.Like(x.Name, pattern))
            .OrderBy(x => x.Name.Length)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (containsMatches.Count > 0)
        {
            return containsMatches;
        }

        return await _dbContext.Places
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<UpsertResult> UpsertRangeAsync(
        IReadOnlyList<Place> places,
        CancellationToken cancellationToken = default
    )
    {
        if (places.Count == 0)
        {
            return new UpsertResult([], 0, 0);
        }

        // Deduplicate input list by ExternalId (keeping latest)
        var deduplicated = places
            .GroupBy(p => p.ExternalId)
            .Select(g => g.Last())
            .ToList();

        var externalIds = deduplicated
            .Select(p => p.ExternalId)
            .Distinct()
            .ToList();

        // Chunk externalIds to avoid SQL Server's 2100 parameter limit
        var existing = new List<Place>();
        foreach (var chunk in externalIds.Chunk(1000))
        {
            var chunkExisting = await _dbContext.Places
                .Where(p => chunk.Contains(p.ExternalId))
                .ToListAsync(cancellationToken);
            existing.AddRange(chunkExisting);
        }

        var byExternalId = existing.ToDictionary(p => p.ExternalId);

        var toInsert = new List<Place>();
        var canonical = new List<Place>(deduplicated.Count);
        var updated = 0;

        foreach (var place in deduplicated)
        {
            if (byExternalId.TryGetValue(place.ExternalId, out var matched))
            {
                matched.UpdateDetails(
                    place.Name,
                    place.Address,
                    place.Category,
                    place.OpeningHours,
                    place.Latitude,
                    place.Longitude
                );
                canonical.Add(matched);
                updated++;
            }
            else
            {
                toInsert.Add(place);
                canonical.Add(place);
            }
        }

        if (toInsert.Count > 0)
        {
            await _dbContext.Places.AddRangeAsync(
                toInsert,
                cancellationToken
            );
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UpsertResult(canonical, toInsert.Count, updated);
    }

    public async Task<(IReadOnlyList<Place> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? category,
        string? source,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = _dbContext.Places.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Name, pattern) ||
                (x.Address != null && EF.Functions.Like(x.Address, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(x => x.Source == source.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<string>> GetDistinctSourcesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Places
            .AsNoTracking()
            .Select(x => x.Source)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetDistinctCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Places
            .AsNoTracking()
            .Where(x => x.Category != null && x.Category != "")
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Place place, CancellationToken cancellationToken = default)
    {
        await _dbContext.Places.AddAsync(place, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Place place, CancellationToken cancellationToken = default)
    {
        _dbContext.Places.Update(place);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Place place, CancellationToken cancellationToken = default)
    {
        var savedPlaces = await _dbContext.SavedPlaces
            .Where(x => x.PlaceId == place.Id)
            .ToListAsync(cancellationToken);
        if (savedPlaces.Count > 0)
        {
            _dbContext.SavedPlaces.RemoveRange(savedPlaces);
        }

        _dbContext.Places.Entry(place).State = EntityState.Deleted;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
