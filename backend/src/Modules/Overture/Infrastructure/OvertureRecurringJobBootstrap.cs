using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Overture.Application.Abstractions;
using Overture.Application.Contracts;
using Overture.Application.Jobs;

namespace Overture.Infrastructure;

public sealed class OvertureRecurringJobBootstrap
{
    public const string SettingsKey = "overture.recurring";
    public const string RecurringJobId = "overture-sync";

    private readonly IRecurringJobManager _recurring;
    private readonly ISettingsRepository _settings;
    private readonly ILogger<OvertureRecurringJobBootstrap> _logger;

    public OvertureRecurringJobBootstrap(
        IRecurringJobManager recurring,
        ISettingsRepository settings,
        ILogger<OvertureRecurringJobBootstrap> logger)
    {
        _recurring = recurring;
        _settings = settings;
        _logger = logger;
    }

    public async Task EnsureScheduledAsync(
        IServiceProvider rootServices,
        CancellationToken cancellationToken = default)
    {
        var setting = await _settings.GetByKeyAsync(SettingsKey, cancellationToken);
        var config = ParseConfig(setting?.ValueJson);

        if (config is null || !config.Enabled)
        {
            _recurring.RemoveIfExists(RecurringJobId);
            _logger.LogInformation(
                "Overture recurring job not enabled; removed if existed.");
            return;
        }

        if (config.AreaId == Guid.Empty)
        {
            _logger.LogWarning(
                "Overture recurring job config is missing AreaId; skipping.");
            return;
        }

        var cron = string.IsNullOrWhiteSpace(config.Cron) ? "0 2 * * *" : config.Cron;
        var batchSize = config.BatchSize <= 0 ? 5000 : config.BatchSize;
        var areaId = config.AreaId;
        var bbox = config.Bbox;

        _recurring.AddOrUpdate<ImportOverturePlacesJob>(
            RecurringJobId,
            job => job.RunAsync(
                new OvertureImportRequest(areaId, batchSize, bbox),
                CancellationToken.None),
            cron);

        _logger.LogInformation(
            "Overture recurring job scheduled: cron={Cron} area={AreaId} batch={Batch}",
            cron, areaId, batchSize);
    }

    private static OvertureImportConfig? ParseConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<OvertureImportConfig>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}