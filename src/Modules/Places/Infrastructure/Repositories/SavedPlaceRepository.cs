using Microsoft.EntityFrameworkCore;
using Places.Application.Abstractions;
using Places.Domain.Entities;
using Places.Infrastructure.Persistence;

namespace Places.Infrastructure.Repositories;

public sealed class SavedPlaceRepository : ISavedPlaceRepository
{
    private readonly PlacesDbContext _dbContext;

    public SavedPlaceRepository(PlacesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SavedPlace?> GetAsync(
        Guid userId,
        Guid placeId,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.SavedPlaces
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.PlaceId == placeId,
                cancellationToken
            );
    }

    public async Task<IReadOnlyList<SavedPlace>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        return await _dbContext.SavedPlaces
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        SavedPlace savedPlace,
        CancellationToken cancellationToken = default
    )
    {
        await _dbContext.SavedPlaces.AddAsync(
            savedPlace,
            cancellationToken
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(
        SavedPlace savedPlace,
        CancellationToken cancellationToken = default
    )
    {
        _dbContext.SavedPlaces.Remove(savedPlace);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
