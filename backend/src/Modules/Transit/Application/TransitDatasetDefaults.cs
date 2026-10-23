using Microsoft.EntityFrameworkCore;
using Transit.Infrastructure;

namespace Transit.Application;

public static class TransitDatasetDefaults
{
    // Reuse an existing active network before creating anything. The same ordering
    // is used for the page and uploads, including installations with legacy sources.
    public static IOrderedQueryable<TransitDataset> Query(IQueryable<TransitDataset> datasets, string mode) =>
        datasets.Where(d => d.Mode == mode)
            .OrderByDescending(d => d.ActiveBatchId != null)
            .ThenBy(d => d.CreatedAt)
            .ThenBy(d => d.Id);

    public static async Task<TransitDataset> GetOrCreate(TransitDbContext db, string mode, CancellationToken ct)
    {
        if (mode is not ("Bus" or "Metro")) throw new ArgumentException("Unsupported transit mode.", nameof(mode));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {"transit:default:" + mode}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
            IF @result < 0 THROW 51000, 'Cannot acquire transit default network lock.', 1;
            """, ct);
        var dataset = await Query(db.Datasets, mode).FirstOrDefaultAsync(ct);
        if (dataset is null)
        {
            dataset = new TransitDataset
            {
                Name = mode == "Bus" ? "Bus" : "Metro", Mode = mode,
                Scope = "Toàn bộ mạng tuyến trong file tải lên"
            };
            db.Datasets.Add(dataset);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return dataset;
    }
}
