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

    public async Task UpsertRangeAsync(
        IReadOnlyList<Place> places,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var place in places)
        {
            var existing = await _dbContext.Places
                .FirstOrDefaultAsync(
                    x => x.ExternalId == place.ExternalId,
                    cancellationToken
                );
            if (existing is null)
            {
                await _dbContext.Places.AddAsync(
                    place,
                    cancellationToken
                );
            }
            else
            {
                existing.UpdateDetails(
                    place.Name,
                    place.Address,
                    place.Category,
                    place.OpeningHours,
                    place.Latitude,
                    place.Longitude
                );
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
