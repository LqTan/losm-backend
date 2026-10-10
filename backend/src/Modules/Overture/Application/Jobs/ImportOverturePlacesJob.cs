using Common.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Overture.Application.Contracts;
using Overture.Infrastructure;
using Places.Application.Abstractions;
using Places.Domain.Entities;

namespace Overture.Application.Jobs;

public sealed class ImportOverturePlacesJob
{
    private readonly IAreaLookup _areas;
    private readonly IPlaceRepository _placeRepository;
    private readonly IOvertureCliRunner _cli;
    private readonly IOvertureItemStreamer _streamer;
    private readonly ILogger<ImportOverturePlacesJob> _logger;

    public ImportOverturePlacesJob(
        IAreaLookup areas,
        IPlaceRepository placeRepository,
        IOvertureCliRunner cli,
        IOvertureItemStreamer streamer,
        ILogger<ImportOverturePlacesJob> logger)
    {
        _areas = areas;
        _placeRepository = placeRepository;
        _cli = cli;
        _streamer = streamer;
        _logger = logger;
    }

    public Task<ImportOvertureResult> RunAsync(
        OvertureImportRequest request,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(request, cancellationToken);

    public async Task<ImportOvertureResult> ExecuteAsync(
        OvertureImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var area = await _areas.GetByIdAsync(request.AreaId, cancellationToken);
        if (area is null)
            throw new NotFoundException($"Area {request.AreaId} not found.");

        var (minLat, minLng, maxLat, maxLng) = request.Bbox is not null
            ? (request.Bbox.MinLat, request.Bbox.MinLng, request.Bbox.MaxLat, request.Bbox.MaxLng)
            : (area.BboxMinLat, area.BboxMinLng, area.BboxMaxLat, area.BboxMaxLng);

        _logger.LogInformation(
            "Overture import starting area={AreaId} bbox=({MinLat},{MinLng})-({MaxLat},{MaxLng}) batch={Batch}",
            request.AreaId, minLat, minLng, maxLat, maxLng, request.BatchSize);

        var download = await _cli.DownloadAsync(
            minLng, minLat, maxLng, maxLat, cancellationToken);

        var batchSize = request.BatchSize <= 0 ? 5000 : request.BatchSize;
        var batch = new List<Place>(batchSize);
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var total = 0;

        try
        {
            await foreach (var item in _streamer
                .StreamAsync(download.OutputPath, cancellationToken)
                .WithCancellation(cancellationToken))
            {
                total++;
                batch.Add(new Place(
                    externalId: item.Id,
                    name: item.Name,
                    latitude: item.Lat,
                    longitude: item.Lng,
                    source: "Overture",
                    address: item.Address,
                    category: item.Category));

                if (batch.Count >= batchSize)
                {
                    var result = await _placeRepository.UpsertRangeAsync(batch, cancellationToken);
                    inserted += result.InsertedCount;
                    updated += result.UpdatedCount;
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                var result = await _placeRepository.UpsertRangeAsync(batch, cancellationToken);
                inserted += result.InsertedCount;
                updated += result.UpdatedCount;
            }
        }
        finally
        {
            try { File.Delete(download.OutputPath); } catch { }
        }

        _logger.LogInformation(
            "Overture import done: total={Total} inserted={Inserted} updated={Updated} skipped={Skipped} duration={Ms}ms",
            total, inserted, updated, skipped, download.DurationMs);

        return new ImportOvertureResult(total, inserted, updated, skipped);
    }
}

public sealed record ImportOvertureResult(int Total, int Inserted, int Updated, int Skipped);

public interface IAreaLookup
{
    Task<AreaSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public sealed record AreaSnapshot(
    Guid Id,
    string Name,
    int Level,
    double BboxMinLat,
    double BboxMinLng,
    double BboxMaxLat,
    double BboxMaxLng);