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

    public async Task<IReadOnlyList<Place>> UpsertRangeAsync(
        IReadOnlyList<Place> places,
        CancellationToken cancellationToken = default
    )
    {
        if (places.Count == 0)
        {
            return [];
        }

        var externalIds = places
            .Select(p => p.ExternalId)
            .Distinct()
            .ToList();

        var existing = await _dbContext.Places
            .Where(p => externalIds.Contains(p.ExternalId))
            .ToListAsync(cancellationToken);

        var byExternalId = existing.ToDictionary(p => p.ExternalId);

        var toInsert = new List<Place>();
        var canonical = new List<Place>(places.Count);

        foreach (var place in places)
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

        return canonical;
    }
}
