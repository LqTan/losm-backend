using Microsoft.EntityFrameworkCore;
namespace Transit.Infrastructure;

// Frozen initial model, also used by the initial migration/snapshot. Future model changes
// belong in a new configuration version; never change this historical definition.
internal static class TransitSchemaV1
{
    public static void Configure(ModelBuilder b)
    {
        b.HasDefaultSchema("transit");
        b.HasAnnotation("ProductVersion", "10.0.11");
        b.UseIdentityColumns();
        b.Entity<TransitDataset>().ToTable("Datasets");
        b.Entity<TransitImportBatch>().ToTable("ImportBatches");
        b.Entity<TransitImportError>().ToTable("ImportErrors");
        b.Entity<TransitRoute>().ToTable("Routes");
        b.Entity<TransitRoutePattern>().ToTable("RoutePatterns");
        b.Entity<TransitStop>().ToTable("Stops");
        b.Entity<TransitRouteStop>().ToTable("RouteStops");
        b.Entity<TransitServiceWindow>().ToTable("ServiceWindows");
        b.Entity<TransitFareRule>().ToTable("FareRules");
        b.Entity<TransitStopSafety>().ToTable("StopSafety");
        b.Entity<TransitActivation>().ToTable("Activations");
        b.Entity<TransitActivation>().HasIndex(x => new { x.DatasetId, x.CreatedAt });
        b.Entity<TransitDataset>().HasKey(x => x.Id);
        b.Entity<TransitDataset>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitDataset>().Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Entity<TransitDataset>().Property(x => x.Mode).HasMaxLength(20).IsRequired();
        b.Entity<TransitDataset>().Property(x => x.Scope).HasMaxLength(500).IsRequired();
        b.Entity<TransitDataset>().Property(x => x.ActiveBatchId);
        b.Entity<TransitDataset>().Property(x => x.CreatedAt).IsRequired();
        b.Entity<TransitImportBatch>().HasKey(x => x.Id);
        b.Entity<TransitImportBatch>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitImportBatch>().Property(x => x.DatasetId).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.BaseBatchId);
        b.Entity<TransitImportBatch>().Property(x => x.Status).HasMaxLength(200).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.FileNames).HasMaxLength(4000).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.CreatedBy).HasMaxLength(200).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.AppliedBy).HasMaxLength(200).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.JobId).HasMaxLength(200);
        b.Entity<TransitImportBatch>().Property(x => x.CreatedAt).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.AppliedAt);
        b.Entity<TransitImportBatch>().Property(x => x.Progress).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.RouteCount).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.StopCount).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.PatternCount).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.RouteStopCount).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.ErrorCount).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.WarningCount).IsRequired();
        b.Entity<TransitImportBatch>().Property(x => x.SummaryJson);
        b.Entity<TransitImportBatch>().Property(x => x.Failure).HasMaxLength(4000);
        b.Entity<TransitImportError>().HasKey(x => x.Id);
        b.Entity<TransitImportError>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitImportError>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitImportError>().Property(x => x.Severity).HasMaxLength(200).IsRequired();
        b.Entity<TransitImportError>().Property(x => x.Sheet).HasMaxLength(200).IsRequired();
        b.Entity<TransitImportError>().Property(x => x.Row).IsRequired();
        b.Entity<TransitImportError>().Property(x => x.Column).HasMaxLength(200).IsRequired();
        b.Entity<TransitImportError>().Property(x => x.Message).HasMaxLength(4000).IsRequired();
        b.Entity<TransitRoute>().HasKey(x => x.Id);
        b.Entity<TransitRoute>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitRoute>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitRoute>().Property(x => x.Number).IsRequired();
        b.Entity<TransitRoute>().Property(x => x.Name).HasMaxLength(500).IsRequired();
        b.Entity<TransitRoute>().Property(x => x.Source).HasMaxLength(500).IsRequired();
        b.Entity<TransitRoute>().Property(x => x.CollectedDate).HasMaxLength(200).IsRequired();
        b.Entity<TransitRoute>().Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Entity<TransitRoutePattern>().HasKey(x => x.Id);
        b.Entity<TransitRoutePattern>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitRoutePattern>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitRoutePattern>().Property(x => x.RouteId).IsRequired();
        b.Entity<TransitRoutePattern>().Property(x => x.Direction).IsRequired();
        b.Entity<TransitRoutePattern>().Property(x => x.PatternCode).HasMaxLength(200).IsRequired();
        b.Entity<TransitRoutePattern>().Property(x => x.Name).HasMaxLength(500).IsRequired();
        b.Entity<TransitRoutePattern>().Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Entity<TransitStop>().HasKey(x => x.Id);
        b.Entity<TransitStop>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitStop>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitStop>().Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Entity<TransitStop>().Property(x => x.Name).HasMaxLength(500).IsRequired();
        b.Entity<TransitStop>().Property(x => x.Latitude).IsRequired();
        b.Entity<TransitStop>().Property(x => x.Longitude).IsRequired();
        b.Entity<TransitStop>().Property(x => x.Address).HasMaxLength(1000).IsRequired();
        b.Entity<TransitStop>().Property(x => x.Code).HasMaxLength(200).IsRequired();
        b.Entity<TransitStop>().Property(x => x.Kind).HasMaxLength(200).IsRequired();
        b.Entity<TransitStop>().Property(x => x.ParentExternalId).HasMaxLength(200);
        b.Entity<TransitRouteStop>().HasKey(x => x.Id);
        b.Entity<TransitRouteStop>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitRouteStop>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitRouteStop>().Property(x => x.PatternId).IsRequired();
        b.Entity<TransitRouteStop>().Property(x => x.StopId).IsRequired();
        b.Entity<TransitRouteStop>().Property(x => x.Sequence).IsRequired();
        b.Entity<TransitServiceWindow>().HasKey(x => x.Id);
        b.Entity<TransitServiceWindow>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitServiceWindow>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitServiceWindow>().Property(x => x.PatternId).IsRequired();
        b.Entity<TransitServiceWindow>().Property(x => x.FirstMinute);
        b.Entity<TransitServiceWindow>().Property(x => x.LastMinute);
        b.Entity<TransitServiceWindow>().Property(x => x.HeadwayMin);
        b.Entity<TransitServiceWindow>().Property(x => x.HeadwayMax);
        b.Entity<TransitServiceWindow>().Property(x => x.ServiceDays).HasMaxLength(200).IsRequired();
        b.Entity<TransitFareRule>().HasKey(x => x.Id);
        b.Entity<TransitFareRule>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitFareRule>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitFareRule>().Property(x => x.RouteId).IsRequired();
        b.Entity<TransitFareRule>().Property(x => x.ReferenceAmount);
        b.Entity<TransitFareRule>().Property(x => x.Currency).HasMaxLength(200).IsRequired();
        b.Entity<TransitFareRule>().Property(x => x.Kind).HasMaxLength(200).IsRequired();
        b.Entity<TransitFareRule>().Property(x => x.Description).HasMaxLength(4000).IsRequired();
        b.Entity<TransitStopSafety>().HasKey(x => x.Id);
        b.Entity<TransitStopSafety>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitStopSafety>().Property(x => x.DatasetId).IsRequired();
        b.Entity<TransitStopSafety>().Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
        b.Entity<TransitStopSafety>().Property(x => x.IsNotSafe).IsRequired();
        b.Entity<TransitStopSafety>().Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Entity<TransitStopSafety>().Property(x => x.UpdatedBy).HasMaxLength(200).IsRequired();
        b.Entity<TransitStopSafety>().Property(x => x.UpdatedAt).IsRequired();
        b.Entity<TransitActivation>().HasKey(x => x.Id);
        b.Entity<TransitActivation>().Property(x => x.Id).IsRequired().ValueGeneratedOnAdd();
        b.Entity<TransitActivation>().Property(x => x.DatasetId).IsRequired();
        b.Entity<TransitActivation>().Property(x => x.BatchId).IsRequired();
        b.Entity<TransitActivation>().Property(x => x.PreviousBatchId);
        b.Entity<TransitActivation>().Property(x => x.Action).HasMaxLength(200).IsRequired();
        b.Entity<TransitActivation>().Property(x => x.Actor).HasMaxLength(200).IsRequired();
        b.Entity<TransitActivation>().Property(x => x.CreatedAt).IsRequired();
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var p in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
                p.SetMaxLength(p.Name switch {
                    "SummaryJson" => (int?)null, "Description" or "Failure" or "Message" or "FileNames" => 4000,
                    "Address" or "Reason" => 1000, "Name" or "Scope" or "Source" => 500, _ => 200 });
        }
        b.Entity<TransitDataset>().HasIndex(x => new { x.Name, x.Mode }).IsUnique();
        b.Entity<TransitImportBatch>().HasOne<TransitDataset>().WithMany().HasForeignKey(x => x.DatasetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitImportBatch>().HasIndex(x => new { x.DatasetId, x.CreatedAt });
        b.Entity<TransitImportError>().HasOne<TransitImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitImportError>().HasIndex(x => new { x.BatchId, x.Severity });
        b.Entity<TransitRoute>().HasOne<TransitImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitRoute>().HasIndex(x => new { x.BatchId, x.Number }).IsUnique();
        b.Entity<TransitRoutePattern>().HasOne<TransitRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitRoutePattern>().HasIndex(x => new { x.RouteId, x.Direction, x.PatternCode }).IsUnique();
        b.Entity<TransitStop>().HasOne<TransitImportBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitStop>().HasIndex(x => new { x.BatchId, x.ExternalId }).IsUnique();
        b.Entity<TransitRouteStop>().HasOne<TransitRoutePattern>().WithMany().HasForeignKey(x => x.PatternId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitRouteStop>().HasOne<TransitStop>().WithMany().HasForeignKey(x => x.StopId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitRouteStop>().HasIndex(x => new { x.PatternId, x.Sequence }).IsUnique();
        b.Entity<TransitRouteStop>().HasIndex(x => x.StopId);
        b.Entity<TransitServiceWindow>().HasOne<TransitRoutePattern>().WithMany().HasForeignKey(x => x.PatternId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitServiceWindow>().HasIndex(x => x.PatternId).IsUnique();
        b.Entity<TransitFareRule>().HasOne<TransitRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitFareRule>().Property(x => x.ReferenceAmount).HasPrecision(18, 2);
        b.Entity<TransitFareRule>().HasIndex(x => x.RouteId);
        b.Entity<TransitStopSafety>().HasOne<TransitDataset>().WithMany().HasForeignKey(x => x.DatasetId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TransitStopSafety>().HasIndex(x => new { x.DatasetId, x.ExternalId }).IsUnique();
        b.Entity<TransitDataset>().Property(x => x.Name).HasMaxLength(200);
        b.Entity<TransitDataset>().Property(x => x.Mode).HasMaxLength(20);
    }
}
