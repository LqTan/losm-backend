using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Transit.Application;
using Transit.Infrastructure;
namespace Transit.Presentation;

[ApiController, Route("api/admin/transit"), Authorize(Roles = "Admin")]
public sealed class TransitAdminController(TransitDbContext db, TransitStorage storage, IBackgroundJobClient jobs) : ControllerBase
{
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "Admin";
    [HttpGet("datasets")]
    public async Task<IActionResult> Datasets(CancellationToken ct) => Ok(await db.Datasets.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));
    [HttpGet("network")]
    public async Task<IActionResult> Network(string mode = "Bus", CancellationToken ct = default)
    {
        if (mode is not ("Bus" or "Metro")) return BadRequest("Loại phương tiện phải là Bus hoặc Metro.");
        return Ok(new { dataset = await TransitDatasetDefaults.Query(db.Datasets.AsNoTracking(), mode).FirstOrDefaultAsync(ct) });
    }
    [HttpPost("imports")]
    [RequestSizeLimit(27 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    public Task<IActionResult> UploadNetwork([FromForm] List<IFormFile> files, string mode = "Bus", CancellationToken ct = default)
    {
        if (mode is not ("Bus" or "Metro")) return Task.FromResult<IActionResult>(BadRequest("Loại phương tiện phải là Bus hoặc Metro."));
        return UploadFiles(null, mode, files, ct);
    }
    [HttpPost("datasets")]
    public async Task<IActionResult> CreateDataset(CreateDatasetRequest request, CancellationToken ct)
    {
        if (request.Mode is not ("Bus" or "Metro")) return BadRequest("Loại phương tiện phải là Bus hoặc Metro.");
        var name = request.Name.Trim(); var scope = request.Scope.Trim();
        if (name.Length == 0 || scope.Length == 0) return BadRequest("Tên nguồn và phạm vi không được trống.");
        var dataset = new TransitDataset { Name = name, Mode = request.Mode, Scope = scope };
        db.Datasets.Add(dataset);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Conflict("Tên nguồn và loại phương tiện đã tồn tại hoặc dữ liệu không hợp lệ."); }
        return Ok(dataset);
    }
    [HttpPost("datasets/{datasetId:guid}/imports")]
    [RequestSizeLimit(27 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 27 * 1024 * 1024)]
    public Task<IActionResult> Upload(Guid datasetId, [FromForm] List<IFormFile> files, CancellationToken ct) =>
        UploadFiles(datasetId, null, files, ct);

    private async Task<IActionResult> UploadFiles(Guid? datasetId, string? mode, List<IFormFile> files, CancellationToken ct)
    {
        if (datasetId.HasValue && !await db.Datasets.AnyAsync(x => x.Id == datasetId, ct)) return NotFound();
        if (files.Count is not (1 or 4) || files.Any(f => f.Length == 0) || files.Sum(f => f.Length) > TransitFileReader.MaxUploadBytes)
            return BadRequest("Chọn một XLSX hoặc đủ bốn CSV; tổng dung lượng tối đa 25 MB, không nhận file rỗng.");
        var names = files.Select(f => Path.GetFileName(f.FileName.Replace('\\', '/')).ToLowerInvariant()).ToArray();
        if (names.Any(n => n.Length > 150)) return BadRequest("Tên file quá dài.");
        var xlsx = files.Count == 1 && names[0].EndsWith(".xlsx");
        if (!xlsx && (files.Count != 4 || names.Any(n => !n.EndsWith(".csv") || !TransitFileReader.TableNames.ContainsKey(Path.GetFileNameWithoutExtension(n))) || names.Select(n => TransitFileReader.TableNames[Path.GetFileNameWithoutExtension(n)]).Distinct().Count() != 4))
            return BadRequest("CSV cần đủ routes.csv, route_patterns.csv, stops.csv, route_stops.csv (hoặc tên sheet tiếng Việt tương ứng).");
        // Validate file selection first; visiting the page or submitting invalid files creates no source.
        datasetId ??= (await TransitDatasetDefaults.GetOrCreate(db, mode!, ct)).Id;
        var batch = new TransitImportBatch { DatasetId = datasetId.Value, FileNames = string.Join(", ", names), CreatedBy = Actor };
        Directory.CreateDirectory(storage.DirectoryFor(batch.Id));
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                await using var output = System.IO.File.Create(storage.FileFor(batch.Id, names[i]));
                await files[i].CopyToAsync(output, ct);
            }
            db.Batches.Add(batch); await db.SaveChangesAsync(ct);
        }
        catch { Directory.Delete(storage.DirectoryFor(batch.Id), true); throw; }
        await QueueValidation(batch, ct);
        return Accepted($"/api/admin/transit/imports/{batch.Id}", batch);
    }
    [HttpGet("imports")]
    public async Task<IActionResult> Imports(Guid datasetId, int page = 1, CancellationToken ct = default)
    {
        var query = db.Batches.AsNoTracking().Where(x => x.DatasetId == datasetId);
        return Ok(new { total = await query.CountAsync(ct), items = await query.OrderByDescending(x => x.CreatedAt).Skip((Math.Clamp(page, 1, 10000) - 1) * 20).Take(20).ToListAsync(ct) });
    }
    [HttpGet("imports/{id:guid}")]
    public async Task<IActionResult> Import(Guid id, CancellationToken ct)
    {
        var batch = await db.Batches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (batch is null) return NotFound();
        return Ok(new { batch, issues = await db.Errors.AsNoTracking().Where(x => x.BatchId == id && x.Severity == "Error").OrderBy(x => x.Severity).ThenBy(x => x.Id).Take(200).ToListAsync(ct) });
    }
    [HttpGet("imports/{id:guid}/errors.csv")]
    public async Task<IActionResult> Errors(Guid id, CancellationToken ct)
    {
        if (!await db.Batches.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var errors = await db.Errors.AsNoTracking().Where(x => x.BatchId == id && x.Severity == "Error").OrderBy(x => x.Id).ToListAsync(ct);
        static string Cell(string s) => "\"" + ((s.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') ? "'" : "") + s.Replace("\"", "\"\"") + "\"";
        var content = "\uFEFFseverity,sheet,row,column,message\r\n" + string.Join("\r\n", errors.Select(e => string.Join(",", Cell(e.Severity), Cell(e.Sheet), e.Row, Cell(e.Column), Cell(e.Message))));
        return File(Encoding.UTF8.GetBytes(content), "text/csv; charset=utf-8", $"transit-errors-{id}.csv");
    }
    [HttpPost("imports/{id:guid}/validate")]
    public async Task<IActionResult> ValidateAgain(Guid id, CancellationToken ct)
    {
        var count = await db.Batches.Where(x => x.Id == id && (x.Status == "ValidationFailed" || x.Status == "Invalid" || x.Status == "Ready" || x.Status == "ApplyFailed"))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Queued").SetProperty(x => x.Progress, 0).SetProperty(x => x.Failure, (string?)null), ct);
        if (count == 0) return Conflict("Đợt import không ở trạng thái có thể kiểm tra lại.");
        var batch = await db.Batches.SingleAsync(x => x.Id == id, ct);
        await QueueValidation(batch, ct); return Accepted(new { batch.Id, batch.Status });
    }
    [HttpPost("imports/{id:guid}/apply")]
    public async Task<IActionResult> Apply(Guid id, ApplyRequest request, CancellationToken ct)
    {
        if (!request.ConfirmFullReplacement) return BadRequest("Cần xác nhận thay toàn bộ dữ liệu của nguồn này sau khi xem chênh lệch.");
        var count = await db.Batches.Where(x => x.Id == id && (x.Status == "Ready" || x.Status == "ApplyFailed") && x.ErrorCount == 0)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ApplyQueued").SetProperty(x => x.AppliedBy, Actor).SetProperty(x => x.Progress, 0).SetProperty(x => x.Failure, (string?)null), ct);
        if (count == 0) return Conflict("Dữ liệu chưa sẵn sàng, có lỗi hoặc đã được áp dụng.");
        try
        {
            var jobId = jobs.Enqueue<TransitImportJobs>(job => job.Apply(id, CancellationToken.None));
            await db.Batches.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.JobId, jobId), ct);
        }
        catch
        {
            await db.Batches.Where(x => x.Id == id && x.Status == "ApplyQueued").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ApplyFailed").SetProperty(x => x.Failure, "Không thể xếp job; hãy thử áp dụng lại."), CancellationToken.None);
            throw;
        }
        return Accepted(new { id });
    }
    [HttpPost("imports/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, RestoreRequest request, CancellationToken ct)
    {
        var batch = await db.Batches.SingleOrDefaultAsync(x => x.Id == id && x.Status == "Applied", ct);
        if (batch is null) return NotFound("Chỉ có thể khôi phục phiên bản đã áp dụng thành công.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TransitImportJobs.LockDataset(db, batch.DatasetId, ct);
        var dataset = await db.Datasets.SingleAsync(x => x.Id == batch.DatasetId, ct);
        if (dataset.ActiveBatchId != request.ExpectedActiveBatchId) return Conflict("Phiên bản đang dùng đã thay đổi; hãy tải lại.");
        dataset.ActiveBatchId = id;
        db.Activations.Add(new() { DatasetId = dataset.Id, BatchId = id, PreviousBatchId = request.ExpectedActiveBatchId, Action = "Restore", Actor = Actor });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(dataset);
    }
    [HttpGet("datasets/{datasetId:guid}/routes")]
    public async Task<IActionResult> Routes(Guid datasetId, string? search, int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == datasetId, ct);
        if (dataset is null) return NotFound();
        var query = db.Routes.AsNoTracking().Where(x => x.BatchId == dataset.ActiveBatchId);
        if (!string.IsNullOrWhiteSpace(search)) { var number = int.TryParse(search, out var n) ? n : -1; query = query.Where(x => x.Name.Contains(search) || x.Number == number); }
        var size = Math.Clamp(pageSize, 1, 200);
        return Ok(new { total = await query.CountAsync(ct), items = await query.OrderBy(x => x.Number).Skip((Math.Clamp(page, 1, 10000) - 1) * size).Take(size).ToListAsync(ct) });
    }
    [HttpGet("routes/{id:guid}")]
    public async Task<IActionResult> Route(Guid id, CancellationToken ct)
    {
        var route = await db.Routes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (route is null) return NotFound();
        var batch = await db.Batches.AsNoTracking().SingleAsync(x => x.Id == route.BatchId, ct);
        var patterns = await db.Patterns.AsNoTracking().Where(x => x.RouteId == id).OrderBy(x => x.Direction).ThenBy(x => x.PatternCode).ToListAsync(ct);
        var stops = await (from rs in db.RouteStops.AsNoTracking()
                           join p in db.Patterns on rs.PatternId equals p.Id
                           join stop in db.Stops on rs.StopId equals stop.Id
                           where p.RouteId == id
                           select new { rs.PatternId, rs.Sequence, stop.Id, stop.ExternalId, stop.Name, stop.Latitude, stop.Longitude, stop.Address,
                               isNotSafe = db.StopSafety.Any(s => s.DatasetId == batch.DatasetId && s.ExternalId == stop.ExternalId && s.IsNotSafe) })
                           .OrderBy(x => x.Sequence).ToListAsync(ct);
        var patternIds = patterns.Select(x => x.Id).ToArray();
        return Ok(new { route, patterns, stops, serviceWindows = await db.ServiceWindows.AsNoTracking().Where(x => patternIds.Contains(x.PatternId)).ToListAsync(ct), fares = await db.FareRules.AsNoTracking().Where(x => x.RouteId == id).ToListAsync(ct) });
    }
    [HttpGet("datasets/{datasetId:guid}/stops")]
    public async Task<IActionResult> Stops(Guid datasetId, string? search, int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        var dataset = await db.Datasets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == datasetId, ct);
        if (dataset is null) return NotFound();
        var query = db.Stops.AsNoTracking().Where(x => x.BatchId == dataset.ActiveBatchId);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.ExternalId.Contains(search) || x.Address.Contains(search));
        var size = Math.Clamp(pageSize, 1, 200);
        return Ok(new { total = await query.CountAsync(ct), items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip((Math.Clamp(page, 1, 10000) - 1) * size).Take(size)
            .Select(x => new { x.Id, x.ExternalId, x.Name, x.Latitude, x.Longitude, x.Address, x.Code, x.Kind, x.ParentExternalId,
                isNotSafe = db.StopSafety.Any(s => s.DatasetId == datasetId && s.ExternalId == x.ExternalId && s.IsNotSafe),
                safetyReason = db.StopSafety.Where(s => s.DatasetId == datasetId && s.ExternalId == x.ExternalId).Select(s => s.Reason).FirstOrDefault() }).ToListAsync(ct) });
    }
    [HttpPut("datasets/{datasetId:guid}/stop-safety")]
    public async Task<IActionResult> Safety(Guid datasetId, SafetyRequest request, CancellationToken ct)
    {
        if (request.IsNotSafe && string.IsNullOrWhiteSpace(request.Reason)) return BadRequest("Cần lý do tọa độ/địa chỉ sai để đánh dấu.");
        if (!await db.Stops.AnyAsync(s => s.ExternalId == request.ExternalId && db.Batches.Any(b => b.Id == s.BatchId && b.DatasetId == datasetId), ct)) return NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await TransitImportJobs.LockDataset(db, datasetId, ct);
        var flag = await db.StopSafety.SingleOrDefaultAsync(x => x.DatasetId == datasetId && x.ExternalId == request.ExternalId, ct);
        if (flag is null) { flag = new() { DatasetId = datasetId, ExternalId = request.ExternalId }; db.StopSafety.Add(flag); }
        flag.IsNotSafe = request.IsNotSafe; flag.Reason = request.Reason.Trim(); flag.UpdatedBy = Actor; flag.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(flag);
    }
    [HttpGet("datasets/{datasetId:guid}/activations")]
    public async Task<IActionResult> Activations(Guid datasetId, CancellationToken ct) => Ok(await db.Activations.AsNoTracking().Where(x => x.DatasetId == datasetId).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct));
    [HttpGet("templates/{table}.csv")]
    public IActionResult Template(string table)
    {
        var headers = table switch
        {
            "routes" => "route_no,route_name,fare,source,collected_date,route_id_site,operation_time,tickets_text,headway_text",
            "route_patterns" => "route_no,direction,direction_name,first_trip,last_trip,headway_min,headway_max,route_var_id,pattern_code,service_days",
            "stops" => "stop_id,stop_name,lat,lon,address,stop_code,stop_kind,parent_stop_id",
            "route_stops" => "route_no,direction,seq,stop_id,stop_name,pattern_code", _ => null
        };
        return headers is null ? NotFound() : File(Encoding.UTF8.GetBytes("\uFEFF" + headers + "\r\n"), "text/csv; charset=utf-8", table + ".csv");
    }
    private async Task QueueValidation(TransitImportBatch batch, CancellationToken ct)
    {
        try
        {
            var id = batch.Id;
            var jobId = jobs.Enqueue<TransitImportJobs>(job => job.Validate(id, CancellationToken.None));
            await db.Batches.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.JobId, jobId), ct);
            batch.JobId = jobId;
        }
        catch
        {
            await db.Batches.Where(x => x.Id == batch.Id && x.Status == "Queued").ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "ValidationFailed").SetProperty(x => x.Failure, "Không thể xếp job; hãy kiểm tra lại."), CancellationToken.None);
            throw;
        }
    }
}
public sealed record CreateDatasetRequest([Required, MaxLength(200)] string Name, [Required] string Mode, [Required, MaxLength(500)] string Scope);
public sealed record ApplyRequest(bool ConfirmFullReplacement);
public sealed record RestoreRequest(Guid? ExpectedActiveBatchId);
public sealed record SafetyRequest([Required, MaxLength(200)] string ExternalId, bool IsNotSafe, [Required(AllowEmptyStrings = true), MaxLength(1000)] string Reason);
