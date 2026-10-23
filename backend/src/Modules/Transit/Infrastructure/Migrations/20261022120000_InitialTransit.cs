using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace Transit.Infrastructure.Migrations;
[DbContext(typeof(TransitDbContext))]
[Migration("20261022120000_InitialTransit")]
public sealed class InitialTransit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
IF SCHEMA_ID(N'transit') IS NULL EXEC(N'CREATE SCHEMA [transit]');
CREATE TABLE [transit].[Datasets] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Mode] nvarchar(20) NOT NULL,
    [Scope] nvarchar(500) NOT NULL,
    [ActiveBatchId] uniqueidentifier NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Datasets] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[ImportBatches] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [BaseBatchId] uniqueidentifier NULL,
    [Status] nvarchar(200) NOT NULL,
    [FileNames] nvarchar(4000) NOT NULL,
    [CreatedBy] nvarchar(200) NOT NULL,
    [AppliedBy] nvarchar(200) NOT NULL,
    [JobId] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [AppliedAt] datetime2 NULL,
    [Progress] int NOT NULL,
    [RouteCount] int NOT NULL,
    [StopCount] int NOT NULL,
    [PatternCount] int NOT NULL,
    [RouteStopCount] int NOT NULL,
    [ErrorCount] int NOT NULL,
    [WarningCount] int NOT NULL,
    [SummaryJson] nvarchar(max) NULL,
    [Failure] nvarchar(4000) NULL,
    CONSTRAINT [PK_ImportBatches] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[ImportErrors] (
    [Id] bigint IDENTITY NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [Severity] nvarchar(200) NOT NULL,
    [Sheet] nvarchar(200) NOT NULL,
    [Row] int NOT NULL,
    [Column] nvarchar(200) NOT NULL,
    [Message] nvarchar(4000) NOT NULL,
    CONSTRAINT [PK_ImportErrors] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[Routes] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [Number] int NOT NULL,
    [Name] nvarchar(500) NOT NULL,
    [Source] nvarchar(500) NOT NULL,
    [CollectedDate] nvarchar(200) NOT NULL,
    [ExternalId] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_Routes] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[RoutePatterns] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [RouteId] uniqueidentifier NOT NULL,
    [Direction] int NOT NULL,
    [PatternCode] nvarchar(200) NOT NULL,
    [Name] nvarchar(500) NOT NULL,
    [ExternalId] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_RoutePatterns] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[Stops] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [ExternalId] nvarchar(200) NOT NULL,
    [Name] nvarchar(500) NOT NULL,
    [Latitude] float NOT NULL,
    [Longitude] float NOT NULL,
    [Address] nvarchar(1000) NOT NULL,
    [Code] nvarchar(200) NOT NULL,
    [Kind] nvarchar(200) NOT NULL,
    [ParentExternalId] nvarchar(200) NULL,
    CONSTRAINT [PK_Stops] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[RouteStops] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [PatternId] uniqueidentifier NOT NULL,
    [StopId] uniqueidentifier NOT NULL,
    [Sequence] int NOT NULL,
    CONSTRAINT [PK_RouteStops] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[ServiceWindows] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [PatternId] uniqueidentifier NOT NULL,
    [FirstMinute] int NULL,
    [LastMinute] int NULL,
    [HeadwayMin] int NULL,
    [HeadwayMax] int NULL,
    [ServiceDays] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_ServiceWindows] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[FareRules] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [RouteId] uniqueidentifier NOT NULL,
    [ReferenceAmount] decimal(18,2) NULL,
    [Currency] nvarchar(200) NOT NULL,
    [Kind] nvarchar(200) NOT NULL,
    [Description] nvarchar(4000) NOT NULL,
    CONSTRAINT [PK_FareRules] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[StopSafety] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [ExternalId] nvarchar(200) NOT NULL,
    [IsNotSafe] bit NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [UpdatedBy] nvarchar(200) NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_StopSafety] PRIMARY KEY ([Id])
);
CREATE TABLE [transit].[Activations] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [PreviousBatchId] uniqueidentifier NULL,
    [Action] nvarchar(200) NOT NULL,
    [Actor] nvarchar(200) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Activations] PRIMARY KEY ([Id])
);
CREATE UNIQUE INDEX [IX_Datasets_Name_Mode] ON [transit].[Datasets] ([Name], [Mode]);
CREATE INDEX [IX_ImportBatches_DatasetId_CreatedAt] ON [transit].[ImportBatches] ([DatasetId], [CreatedAt]);
CREATE INDEX [IX_ImportErrors_BatchId_Severity] ON [transit].[ImportErrors] ([BatchId], [Severity]);
CREATE UNIQUE INDEX [IX_Routes_BatchId_Number] ON [transit].[Routes] ([BatchId], [Number]);
CREATE UNIQUE INDEX [IX_RoutePatterns_RouteId_Direction_PatternCode] ON [transit].[RoutePatterns] ([RouteId], [Direction], [PatternCode]);
CREATE UNIQUE INDEX [IX_Stops_BatchId_ExternalId] ON [transit].[Stops] ([BatchId], [ExternalId]);
CREATE UNIQUE INDEX [IX_RouteStops_PatternId_Sequence] ON [transit].[RouteStops] ([PatternId], [Sequence]);
CREATE INDEX [IX_RouteStops_StopId] ON [transit].[RouteStops] ([StopId]);
CREATE UNIQUE INDEX [IX_ServiceWindows_PatternId] ON [transit].[ServiceWindows] ([PatternId]);
CREATE INDEX [IX_FareRules_RouteId] ON [transit].[FareRules] ([RouteId]);
CREATE UNIQUE INDEX [IX_StopSafety_DatasetId_ExternalId] ON [transit].[StopSafety] ([DatasetId], [ExternalId]);
CREATE INDEX [IX_Activations_DatasetId_CreatedAt] ON [transit].[Activations] ([DatasetId], [CreatedAt]);
ALTER TABLE [transit].[ImportBatches] ADD CONSTRAINT [FK_ImportBatches_Datasets_DatasetId] FOREIGN KEY ([DatasetId]) REFERENCES [transit].[Datasets] ([Id]);
ALTER TABLE [transit].[ImportErrors] ADD CONSTRAINT [FK_ImportErrors_ImportBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [transit].[ImportBatches] ([Id]);
ALTER TABLE [transit].[Routes] ADD CONSTRAINT [FK_Routes_ImportBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [transit].[ImportBatches] ([Id]);
ALTER TABLE [transit].[RoutePatterns] ADD CONSTRAINT [FK_RoutePatterns_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [transit].[Routes] ([Id]);
ALTER TABLE [transit].[Stops] ADD CONSTRAINT [FK_Stops_ImportBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [transit].[ImportBatches] ([Id]);
ALTER TABLE [transit].[RouteStops] ADD CONSTRAINT [FK_RouteStops_RoutePatterns_PatternId] FOREIGN KEY ([PatternId]) REFERENCES [transit].[RoutePatterns] ([Id]);
ALTER TABLE [transit].[RouteStops] ADD CONSTRAINT [FK_RouteStops_Stops_StopId] FOREIGN KEY ([StopId]) REFERENCES [transit].[Stops] ([Id]);
ALTER TABLE [transit].[ServiceWindows] ADD CONSTRAINT [FK_ServiceWindows_RoutePatterns_PatternId] FOREIGN KEY ([PatternId]) REFERENCES [transit].[RoutePatterns] ([Id]);
ALTER TABLE [transit].[FareRules] ADD CONSTRAINT [FK_FareRules_Routes_RouteId] FOREIGN KEY ([RouteId]) REFERENCES [transit].[Routes] ([Id]);
ALTER TABLE [transit].[StopSafety] ADD CONSTRAINT [FK_StopSafety_Datasets_DatasetId] FOREIGN KEY ([DatasetId]) REFERENCES [transit].[Datasets] ([Id]);
""");
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
DROP TABLE [transit].[Activations];
DROP TABLE [transit].[StopSafety];
DROP TABLE [transit].[FareRules];
DROP TABLE [transit].[ServiceWindows];
DROP TABLE [transit].[RouteStops];
DROP TABLE [transit].[Stops];
DROP TABLE [transit].[RoutePatterns];
DROP TABLE [transit].[Routes];
DROP TABLE [transit].[ImportErrors];
DROP TABLE [transit].[ImportBatches];
DROP TABLE [transit].[Datasets];
""");
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => TransitSchemaV1.Configure(modelBuilder);
}
