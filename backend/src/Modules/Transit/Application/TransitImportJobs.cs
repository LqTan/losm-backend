using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Transit.Infrastructure;
namespace Transit.Application;

public sealed class TransitImportJobs(TransitDbContext db, TransitStorage storage, TransitValidator validator, JobStorage jobs, ILogger<TransitImportJobs> logger)
{
    [AutomaticRetry(Attempts = 2)]
    public async Task Validate(Guid id, CancellationToken ct)
    {
        using var connection = jobs.GetConnection();
        using var lease = connection.AcquireDistributedLock($"transit:batch:{id}", TimeSpan.FromSeconds(15));
        var batch = await db.Batches.SingleAsync(x => x.Id == id, ct);
        if (batch.Status is not ("Queued" or "Validating" or "ValidationFailed")) return;
        try
        {
            batch.Status = "Validating"; batch.Progress = 10; batch.Failure = null;
            await db.SaveChangesAsync(ct);
            var dataset = await db.Datasets.AsNoTracking().SingleAsync(x => x.Id == batch.DatasetId, ct);
            var snapshot = validator.Validate(TransitFileReader.Read(storage.UploadedFiles(id), ct), id, dataset.Mode, ct);
            batch.Progress = 70;
            await storage.WriteSnapshot(id, snapshot, ct);
            await db.Errors.Where(x => x.BatchId == id).ExecuteDeleteAsync(ct);
            db.Errors.AddRange(snapshot.Issues);
            batch.RouteCount = snapshot.Routes.Count; batch.StopCount = snapshot.Stops.Count;
            batch.PatternCount = snapshot.Patterns.Count; batch.RouteStopCount = snapshot.RouteStops.Count;
            batch.ErrorCount = snapshot.Issues.Count(x => x.Severity == "Error");
            batch.WarningCount = snapshot.Issues.Count(x => x.Severity == "Warning");
            batch.BaseBatchId = dataset.ActiveBatchId;
            batch.SummaryJson = await Preview(snapshot, dataset.ActiveBatchId, ct);
            batch.Status = batch.ErrorCount == 0 ? "Ready" : "Invalid";
            batch.Progress = 100;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Transit validation failed for {BatchId}", id);
            db.ChangeTracker.Clear();
            var message = ex is InvalidDataException or FormatException or System.Xml.XmlException or Microsoft.VisualBasic.FileIO.MalformedLineException
                ? ex.Message : "Không thể kiểm tra file. Xem log job và thử lại.";
            message = message.Length > 3900 ? message[..3900] : message;
            await db.Batches.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ValidationFailed").SetProperty(x => x.Failure, message), ct);
            throw;
        }
    }
    [AutomaticRetry(Attempts = 2)]
    public async Task Apply(Guid id, CancellationToken ct)
    {
        using var connection = jobs.GetConnection();
        using var lease = connection.AcquireDistributedLock($"transit:batch:{id}", TimeSpan.FromSeconds(15));
        var batch = await db.Batches.SingleAsync(x => x.Id == id, ct);
        if (batch.Status is not ("ApplyQueued" or "Applying" or "ApplyFailed")) return;
        try
        {
            batch.Status = "Applying"; batch.Progress = 10; batch.Failure = null;
            await db.SaveChangesAsync(ct);
            var snapshot = await storage.ReadSnapshot(id, ct);
            if (snapshot.Issues.Any(x => x.Severity == "Error") || snapshot.Routes.Count == 0 || snapshot.Stops.Count == 0)
                throw new InvalidDataException("Dữ liệu chưa vượt qua kiểm tra.");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockDataset(db, batch.DatasetId, ct);
            var dataset = await db.Datasets.SingleAsync(x => x.Id == batch.DatasetId, ct);
            if (dataset.ActiveBatchId != batch.BaseBatchId)
                throw new InvalidDataException("Phiên bản đang dùng đã thay đổi. Bấm kiểm tra lại để xem chênh lệch mới trước khi áp dụng.");
            db.Routes.AddRange(snapshot.Routes); db.Stops.AddRange(snapshot.Stops);
            db.Patterns.AddRange(snapshot.Patterns); db.RouteStops.AddRange(snapshot.RouteStops);
            db.ServiceWindows.AddRange(snapshot.ServiceWindows); db.FareRules.AddRange(snapshot.FareRules);
            await db.SaveChangesAsync(ct);
            db.Activations.Add(new() { DatasetId = dataset.Id, BatchId = id, PreviousBatchId = dataset.ActiveBatchId, Actor = batch.AppliedBy });
            dataset.ActiveBatchId = id; batch.Status = "Applied"; batch.Progress = 100; batch.AppliedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Transit apply failed for {BatchId}", id);
            db.ChangeTracker.Clear();
            var message = ex is InvalidDataException ? ex.Message : "Áp dụng chưa hoàn tất; phiên bản trước vẫn được giữ nguyên. Có thể thử lại.";
            await db.Batches.Where(x => x.Id == id && x.Status != "Applied").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ApplyFailed").SetProperty(x => x.Failure, message), ct);
            throw;
        }
    }
    public static Task LockDataset(TransitDbContext db, Guid id, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sp_getapplock @Resource = {"transit:dataset:" + id}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
        IF @result < 0 THROW 51000, 'Cannot acquire transit dataset lock.', 1;
        """, ct);
    private async Task<string> Preview(TransitSnapshot snapshot, Guid? active, CancellationToken ct)
    {
        var oldRoutes = await db.Routes.AsNoTracking().Where(x => x.BatchId == active).ToListAsync(ct);
        var oldStops = await db.Stops.AsNoTracking().Where(x => x.BatchId == active).ToListAsync(ct);
        var routeKeys = snapshot.Routes.Select(x => x.Number).ToHashSet();
        var stopKeys = snapshot.Stops.Select(x => x.ExternalId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previousRoutes = oldRoutes.Select(x => x.Number).ToHashSet();
        var previousStops = oldStops.Select(x => x.ExternalId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(new
        {
            routes = new { added = routeKeys.Except(previousRoutes).Count(), replaced = routeKeys.Intersect(previousRoutes).Count(), removed = previousRoutes.Except(routeKeys).Count() },
            stops = new { added = stopKeys.Except(previousStops, StringComparer.OrdinalIgnoreCase).Count(), replaced = stopKeys.Intersect(previousStops, StringComparer.OrdinalIgnoreCase).Count(), removed = previousStops.Except(stopKeys, StringComparer.OrdinalIgnoreCase).Count() },
            policy = "FullSnapshot", note = "Thay toàn bộ dữ liệu của nguồn này; phiên bản cũ được giữ để khôi phục."
        });
    }
}
