namespace Transit.Infrastructure;

public sealed class TransitDataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Mode { get; set; } = "Bus";
    public string Scope { get; set; } = "";
    public Guid? ActiveBatchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class TransitImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public Guid? BaseBatchId { get; set; }
    public string Status { get; set; } = "Queued";
    public string FileNames { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string AppliedBy { get; set; } = "";
    public string? JobId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAt { get; set; }
    public int Progress { get; set; }
    public int RouteCount { get; set; }
    public int StopCount { get; set; }
    public int PatternCount { get; set; }
    public int RouteStopCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public string? SummaryJson { get; set; }
    public string? Failure { get; set; }
}
public sealed class TransitImportError
{
    public long Id { get; set; }
    public Guid BatchId { get; set; }
    public string Severity { get; set; } = "Error";
    public string Sheet { get; set; } = "";
    public int Row { get; set; }
    public string Column { get; set; } = "";
    public string Message { get; set; } = "";
}
public sealed class TransitRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public string CollectedDate { get; set; } = "";
    public string ExternalId { get; set; } = "";
}
public sealed class TransitRoutePattern
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public Guid RouteId { get; set; }
    public int Direction { get; set; }
    public string PatternCode { get; set; } = "default";
    public string Name { get; set; } = "";
    public string ExternalId { get; set; } = "";
}
public sealed class TransitStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public string ExternalId { get; set; } = "";
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = "";
    public string Code { get; set; } = "";
    public string Kind { get; set; } = "Stop";
    public string? ParentExternalId { get; set; }
}
public sealed class TransitRouteStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public Guid PatternId { get; set; }
    public Guid StopId { get; set; }
    public int Sequence { get; set; }
}
public sealed class TransitServiceWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public Guid PatternId { get; set; }
    public int? FirstMinute { get; set; }
    public int? LastMinute { get; set; }
    public int? HeadwayMin { get; set; }
    public int? HeadwayMax { get; set; }
    public string ServiceDays { get; set; } = "Unknown";
}
public sealed class TransitFareRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BatchId { get; set; }
    public Guid RouteId { get; set; }
    public decimal? ReferenceAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public string Kind { get; set; } = "Unverified";
    public string Description { get; set; } = "";
}
// Independent of imported versions: replacement/rollback must never clear a safety flag.
public sealed class TransitStopSafety
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public string ExternalId { get; set; } = "";
    public bool IsNotSafe { get; set; }
    public string Reason { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TransitActivation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public Guid BatchId { get; set; }
    public Guid? PreviousBatchId { get; set; }
    public string Action { get; set; } = "Apply";
    public string Actor { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
