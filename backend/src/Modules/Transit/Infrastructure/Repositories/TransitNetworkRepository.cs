using Microsoft.EntityFrameworkCore;
using Transit.Application.Abstractions;
using Transit.Domain.Models;
namespace Transit.Infrastructure.Repositories;

public sealed class TransitNetworkRepository(TransitDbContext db) : ITransitNetworkRepository
{
    public async Task<TransitNetwork> ReadActiveAsync(string? mode, CancellationToken ct = default)
    {
        // Pin published versions for the entire read. Historical rows remain available
        // when an administrator switches the active version during this request.
        var datasets = await (from d in db.Datasets.AsNoTracking()
                              join b in db.Batches.AsNoTracking() on d.ActiveBatchId equals b.Id
                              where b.Status == "Applied" && b.DatasetId == d.Id && (mode == null || d.Mode == mode)
                              select new { d.Id, d.Mode, BatchId = b.Id }).ToListAsync(ct);
        if (datasets.Count == 0) return new([], [], []);
        var batchIds = datasets.Select(x => x.BatchId).ToArray();
        var datasetIds = datasets.Select(x => x.Id).ToArray();
        var sources = datasets.ToDictionary(x => x.BatchId);
        var flags = await db.StopSafety.AsNoTracking().Where(x => datasetIds.Contains(x.DatasetId) && x.IsNotSafe).ToListAsync(ct);
        var unsafeKeys = flags.Select(x => (x.DatasetId, x.ExternalId.ToUpperInvariant())).ToHashSet();
        var stops = await db.Stops.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).ToListAsync(ct);
        var routes = await db.Routes.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).ToListAsync(ct);
        var patterns = await db.Patterns.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).ToListAsync(ct);
        var calls = await db.RouteStops.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).OrderBy(x => x.Sequence).ToListAsync(ct);
        var windows = await db.ServiceWindows.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).ToListAsync(ct);
        var fares = await db.FareRules.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).ToListAsync(ct);
        var byPattern = calls.ToLookup(x => x.PatternId);
        return new(
            stops.Select(s => new NetworkStop(s.Id, s.ExternalId, s.Name, s.Address, s.Latitude, s.Longitude,
                sources[s.BatchId].Mode, unsafeKeys.Contains((sources[s.BatchId].Id, s.ExternalId.ToUpperInvariant())))).ToArray(),
            routes.Select(r => new NetworkRoute(r.Id, r.Number, r.Name, sources[r.BatchId].Mode,
                fares.FirstOrDefault(f => f.RouteId == r.Id)?.ReferenceAmount,
                fares.FirstOrDefault(f => f.RouteId == r.Id)?.Description ?? "")).ToArray(),
            patterns.Select(p => {
                var w = windows.FirstOrDefault(x => x.PatternId == p.Id);
                return new NetworkPattern(p.Id, p.RouteId, p.Direction, p.Name, w?.FirstMinute, w?.LastMinute,
                    w?.HeadwayMin, w?.HeadwayMax, w?.ServiceDays ?? "Unknown",
                    byPattern[p.Id].Select(c => new NetworkCall(c.StopId, c.Sequence)).ToArray());
            }).ToArray());
    }
}
