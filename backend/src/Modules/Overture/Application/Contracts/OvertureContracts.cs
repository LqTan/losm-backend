namespace Overture.Application.Contracts;

public sealed record OvertureImportConfig(
    Guid AreaId,
    string Cron,
    int BatchSize,
    bool Enabled,
    BoundingBoxPayload? Bbox = null);

public sealed record OvertureImportRequest(
    Guid AreaId,
    int BatchSize,
    BoundingBoxPayload? Bbox);

public sealed record BoundingBoxPayload(
    double MinLat,
    double MinLng,
    double MaxLat,
    double MaxLng);

public sealed record OvertureImportResponse(
    string JobId,
    Guid AreaId,
    int BatchSize,
    DateTime EnqueuedAt);

public sealed record SettingResponse(
    string Key,
    string ValueJson,
    DateTime UpdatedAt,
    Guid? UpdatedBy);

public sealed record OvertureRecurringConfig(
    Guid AreaId,
    string Cron,
    int BatchSize,
    bool Enabled,
    BoundingBoxPayload? Bbox);

public sealed record UpdateSettingCommand(
    string Key,
    string ValueJson,
    Guid? UpdatedBy);