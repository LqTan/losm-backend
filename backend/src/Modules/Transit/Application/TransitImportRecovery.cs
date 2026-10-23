using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Transit.Infrastructure;
namespace Transit.Application;

// Repairs the small SQL/Hangfire enqueue gap after a process crash. Re-enqueued jobs
// are safe: each batch holds a distributed lock and applied batches are immutable.
public sealed class TransitImportRecovery(TransitDbContext db, JobStorage storage, IBackgroundJobClient jobs)
{
    [AutomaticRetry(Attempts = 1)]
    public async Task Run(CancellationToken ct)
    {
        using var connection = storage.GetConnection();
        using var lease = connection.AcquireDistributedLock("transit:recovery", TimeSpan.FromSeconds(15));
        var cutoff = DateTime.UtcNow.AddMinutes(-2);
        var pending = await db.Batches.AsNoTracking().Where(b => b.CreatedAt < cutoff &&
            (b.Status == "Queued" || b.Status == "Validating" || b.Status == "ApplyQueued" || b.Status == "Applying"))
            .OrderBy(b => b.CreatedAt).Take(100).ToListAsync(ct);
        foreach (var batch in pending)
        {
            ct.ThrowIfCancellationRequested();
            var state = batch.JobId is null ? null : connection.GetStateData(batch.JobId)?.Name;
            if (state is "Processing" or "Enqueued" or "Scheduled" or "Awaiting") continue;
            var apply = batch.Status is "ApplyQueued" or "Applying";
            if (state == "Failed")
            {
                await db.Batches.Where(b => b.Id == batch.Id && b.Status == batch.Status).ExecuteUpdateAsync(s =>
                    s.SetProperty(b => b.Status, apply ? "ApplyFailed" : "ValidationFailed")
                     .SetProperty(b => b.Failure, "Job đã hết số lần thử lại. Xem chi tiết Hangfire hoặc thử lại từ trang import."), ct);
                continue;
            }
            var id = batch.Id;
            var jobId = apply ? jobs.Enqueue<TransitImportJobs>(j => j.Apply(id, CancellationToken.None))
                : jobs.Enqueue<TransitImportJobs>(j => j.Validate(id, CancellationToken.None));
            await db.Batches.Where(b => b.Id == id && b.Status == batch.Status).ExecuteUpdateAsync(s => s.SetProperty(b => b.JobId, jobId), ct);
        }
    }
}
public sealed class TransitImportRecoverySchedule(IRecurringJobManager recurring) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurring.AddOrUpdate<TransitImportRecovery>("transit-import-recovery", job => job.Run(CancellationToken.None), "*/2 * * * *");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
