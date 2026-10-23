using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Transit.Application;
using Transit.Infrastructure;
namespace Transit.Tests;

public sealed class TransitImportTests
{
    private static List<FileTable> Tables() => [
        Table("routes", "route_no,route_name,fare,tickets_text", "01,Example route,5000,Adult fare"),
        Table("route_patterns", "route_no,direction,direction_name,first_trip,last_trip,headway_min,headway_max", "1,0,Outbound,05:00,22:00,8,20"),
        Table("stops", "stop_id,stop_name,lat,lon,parent_stop_id", "S1,First,10.77,106.70,", "S2,Second,10.78,106.71,"),
        Table("route_stops", "route_no,direction,seq,stop_id", "01,0,1,S1", "1,0,2,S2")
    ];
    private static FileTable Table(string name, string header, params string[] rows)
    {
        var columns = header.Split(',');
        return new(name, rows.Select((r, i) => new FileRow(i + 2, columns.Zip(r.Split(',')).ToDictionary(x => x.First, x => x.Second))).ToArray(), columns.ToHashSet());
    }
    private static TransitSnapshot Validate(List<FileTable> tables, string mode = "Bus") => new TransitValidator().Validate(tables, Guid.NewGuid(), mode);
    [Fact]
    public void DefaultNetworkReusesExistingActiveDataWithinTheRequestedMode()
    {
        var unusedBus = new TransitDataset { Mode = "Bus", CreatedAt = new DateTime(2026, 1, 1) };
        var activeBus = new TransitDataset { Mode = "Bus", ActiveBatchId = Guid.NewGuid(), CreatedAt = new DateTime(2026, 2, 1) };
        var metro = new TransitDataset { Mode = "Metro", ActiveBatchId = Guid.NewGuid(), CreatedAt = new DateTime(2025, 1, 1) };
        var datasets = new[] { unusedBus, metro, activeBus }.AsQueryable();
        Assert.Same(activeBus, TransitDatasetDefaults.Query(datasets, "Bus").First());
        Assert.Same(metro, TransitDatasetDefaults.Query(datasets, "Metro").First());
    }
    [Fact]
    public void DefaultNetworkSelectionIsStableAndReusesAnEmptyLegacySource()
    {
        var old = new TransitDataset { Mode = "Bus", CreatedAt = new DateTime(2026, 1, 1) };
        var newer = new TransitDataset { Mode = "Bus", CreatedAt = new DateTime(2026, 2, 1) };
        Assert.Same(old, TransitDatasetDefaults.Query(new[] { newer, old }.AsQueryable(), "Bus").First());
        Assert.Same(old, TransitDatasetDefaults.Query(new[] { old, newer }.AsQueryable(), "Bus").First());
    }
    [Fact]
    public void EmptyNetworkLookupDoesNotRequireCreatingASource()
    {
        Assert.Null(TransitDatasetDefaults.Query(Array.Empty<TransitDataset>().AsQueryable(), "Bus").FirstOrDefault());
    }
    [Fact]
    public void NumericCodesAreNormalizedAcrossAllTables()
    {
        var result = Validate(Tables());
        Assert.Empty(result.Issues);
        Assert.Equal(1, Assert.Single(result.Routes).Number);
        Assert.All(result.Patterns, p => Assert.Equal(result.Routes[0].Id, p.RouteId));
        Assert.Equal(2, result.RouteStops.Count);
        Assert.Equal("Unverified", Assert.Single(result.FareRules).Kind);
        Assert.Equal("Unknown", Assert.Single(result.ServiceWindows).ServiceDays);
    }
    [Fact]
    public void DuplicateNormalizedNumbersAreRejected()
    {
        var tables = Tables(); tables[0] = Table("routes", "route_no,route_name", "01,First", "1,Duplicate");
        Assert.Contains(Validate(tables).Issues, e => e.Column == "route_no" && e.Message.Contains("Trùng"));
    }
    [Fact]
    public void AlphanumericCodesAreNotSilentlyConverted()
    {
        var tables = Tables(); tables[0].Rows[0].Cells["route_no"] = "DL01";
        Assert.Contains(Validate(tables).Issues, e => e.Sheet == "routes" && e.Column == "route_no" && e.Severity == "Error");
    }
    [Fact]
    public void MissingStopAndWrongDirectionAreReportedWithRowNumbers()
    {
        var tables = Tables(); tables[3].Rows[0].Cells["stop_id"] = "missing"; tables[3].Rows[1].Cells["direction"] = "1";
        var result = Validate(tables);
        Assert.Contains(result.Issues, e => e.Sheet == "route_stops" && e.Row == 2 && e.Column == "stop_id");
        Assert.Contains(result.Issues, e => e.Sheet == "route_stops" && e.Row == 3 && e.Column == "route_no");
    }
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("91")]
    [InlineData("")]
    public void InvalidLatitudeIsRejected(string latitude)
    {
        var tables = Tables(); tables[2].Rows[0].Cells["lat"] = latitude;
        Assert.Contains(Validate(tables).Issues, e => e.Sheet == "stops" && e.Column == "lat");
    }
    [Fact]
    public void RepeatedStopInLoopIsAllowedButRepeatedSequenceIsNot()
    {
        var tables = Tables(); tables[3] = Table("route_stops", "route_no,direction,seq,stop_id", "1,0,1,S1", "1,0,2,S2", "1,0,3,S1");
        Assert.DoesNotContain(Validate(tables).Issues, e => e.Severity == "Error");
        tables[3].Rows[2].Cells["seq"] = "2";
        Assert.Contains(Validate(tables).Issues, e => e.Column == "seq" && e.Row == 4);
    }
    [Fact]
    public void MetroParentCyclesAreRejected()
    {
        var tables = Tables(); tables[2].Rows[0].Cells["parent_stop_id"] = "S2"; tables[2].Rows[1].Cells["parent_stop_id"] = "S1";
        var result = Validate(tables, "Metro");
        Assert.All(result.Stops, s => Assert.Equal("Station", s.Kind));
        Assert.Contains(result.Issues, e => e.Column == "parent_stop_id" && e.Message.Contains("vòng lặp"));
    }
    [Fact]
    public void ExcelTimeSerialAndOvernightServiceAreSupported()
    {
        var tables = Tables(); tables[1].Rows[0].Cells["first_trip"] = "0.875"; tables[1].Rows[0].Cells["last_trip"] = "01:00";
        var result = Validate(tables);
        Assert.DoesNotContain(result.Issues, e => e.Severity == "Error");
        Assert.Equal(1260, result.ServiceWindows[0].FirstMinute); Assert.Equal(60, result.ServiceWindows[0].LastMinute);
    }
    [Fact]
    public void PatternBranchesHaveIndependentStopSequences()
    {
        var tables = Tables();
        tables[1] = Table("route_patterns", "route_no,direction,direction_name,pattern_code", "1,0,Branch A,A", "1,0,Branch B,B");
        tables[3] = Table("route_stops", "route_no,direction,seq,stop_id,pattern_code", "1,0,1,S1,a", "1,0,2,S2,a", "1,0,1,S2,b", "1,0,2,S1,b");
        var result = Validate(tables);
        Assert.Empty(result.Issues); // Optional schedules do not produce import warnings.
        Assert.Equal(2, result.Patterns.Count);
    }
    [Fact]
    public void CsvHandlesQuotedCommasMultilineAndBom()
    {
        using var folder = new TempFolder();
        WriteCsv(folder.Path, Tables());
        File.WriteAllText(System.IO.Path.Combine(folder.Path, "routes.csv"), "\uFEFFroute_no,route_name,fare,tickets_text\r\n01,\"Route, with comma\",5000,\"Line 1\r\nLine 2\"\r\n", Encoding.UTF8);
        var result = Validate(TransitFileReader.Read(Directory.GetFiles(folder.Path)));
        Assert.DoesNotContain(result.Issues, e => e.Severity == "Error");
        Assert.Equal("Route, with comma", result.Routes[0].Name);
        Assert.Contains("Line 2", result.FareRules[0].Description);
    }
    [Fact]
    public void WorkbookAndCsvHaveEquivalentResults()
    {
        using var folder = new TempFolder();
        var tables = Tables(); WriteCsv(folder.Path, tables);
        var csv = Validate(TransitFileReader.Read(Directory.GetFiles(folder.Path)));
        var path = System.IO.Path.Combine(folder.Path, "data.xlsx"); WriteWorkbook(path, tables);
        var xlsx = Validate(TransitFileReader.Read([path]));
        Assert.DoesNotContain(xlsx.Issues, e => e.Severity == "Error");
        Assert.Equal(csv.Routes.Select(r => (r.Number, r.Name)), xlsx.Routes.Select(r => (r.Number, r.Name)));
        Assert.Equal(csv.Stops.Select(s => (s.ExternalId, s.Latitude, s.Longitude)), xlsx.Stops.Select(s => (s.ExternalId, s.Latitude, s.Longitude)));
        Assert.Equal(csv.RouteStops.Count, xlsx.RouteStops.Count);
    }
    [Fact]
    public void MissingCsvAndDuplicateSheetsAreRejected()
    {
        using var folder = new TempFolder(); WriteCsv(folder.Path, Tables());
        Assert.Throws<InvalidDataException>(() => TransitFileReader.Read(Directory.GetFiles(folder.Path).Take(3)));
        var path = System.IO.Path.Combine(folder.Path, "data.xlsx");
        WriteWorkbook(path, [Tables()[0], Tables()[0], Tables()[2], Tables()[3]]);
        Assert.Throws<InvalidDataException>(() => TransitFileReader.Read([path]));
    }
    [Fact]
    public void MigrationMatchesCurrentModelWithoutConnectingToDatabase()
    {
        using var db = new TransitDbContext(new DbContextOptionsBuilder<TransitDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true", o => o.MigrationsHistoryTable("__EFMigrationsHistory", "transit")).Options);
        var historical = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var initialized = db.GetService<IModelRuntimeInitializer>().Initialize(historical, designTime: true);
        var current = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(initialized.GetRelationalModel(), current.GetRelationalModel());
        var diagnostic = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(differences, current).Select(c => c.CommandText));
        Assert.True(!db.Database.HasPendingModelChanges(), diagnostic);
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE [transit].[Routes]", script);
        Assert.Contains("[Number] int NOT NULL", script);
        Assert.Contains("[FK_RouteStops_Stops_StopId]", script);
        Assert.Contains("[transit].[__EFMigrationsHistory]", script);
    }
    [ProvidedWorkbookFact]
    public void ProvidedWorkbookImportsAndMatchesCsv()
    {
        var path = Environment.GetEnvironmentVariable("TRANSIT_WORKBOOK")!;
        var tables = TransitFileReader.Read([path]);
        var result = Validate(tables);
        Assert.True(!result.Issues.Any(x => x.Severity == "Error"), string.Join("\n", result.Issues.Where(x => x.Severity == "Error").Select(x => $"{x.Sheet}:{x.Row}:{x.Column} {x.Message}")));
        Assert.Empty(result.Issues); // Reference fares in the supplied workbook are not import errors/warnings.
        Assert.Equal(39, result.Routes.Count);
        Assert.Equal(78, result.Patterns.Count);
        Assert.Equal(2155, result.Stops.Count);
        Assert.Equal(4160, result.RouteStops.Count);
        using var folder = new TempFolder();
        WriteCsv(folder.Path, tables);
        var csv = Validate(TransitFileReader.Read(Directory.GetFiles(folder.Path)));
        Assert.DoesNotContain(csv.Issues, x => x.Severity == "Error");
        Assert.Equal(result.Routes.Select(r => (r.Number, r.Name)), csv.Routes.Select(r => (r.Number, r.Name)));
        Assert.Equal(result.Stops.Select(r => (r.ExternalId, r.Latitude, r.Longitude)), csv.Stops.Select(r => (r.ExternalId, r.Latitude, r.Longitude)));
        Assert.Equal(result.RouteStops.Count, csv.RouteStops.Count);
    }
    private static void WriteCsv(string dir, List<FileTable> tables)
    {
        foreach (var table in tables)
        {
            var headers = table.Headers.ToArray();
            File.WriteAllText(System.IO.Path.Combine(dir, table.Name + ".csv"), string.Join(',', headers) + "\n" + string.Join('\n', table.Rows.Select(r => string.Join(',', headers.Select(h => "\"" + r.Get(h).Replace("\"", "\"\"") + "\"")))));
        }
    }
    private static void WriteWorkbook(string path, List<FileTable> tables)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Add(string name, XDocument doc) { using var stream = zip.CreateEntry(name).Open(); doc.Save(stream); }
        Add("xl/workbook.xml", new(new XElement(ns + "workbook", new XElement(ns + "sheets", tables.Select((t, i) => new XElement(ns + "sheet", new XAttribute("name", t.Name), new XAttribute(rel + "id", "r" + i)))))));
        Add("xl/_rels/workbook.xml.rels", new(new XElement(pkg + "Relationships", tables.Select((t, i) => new XElement(pkg + "Relationship", new XAttribute("Id", "r" + i), new XAttribute("Target", "worksheets/sheet" + i + ".xml"))))));
        for (var i = 0; i < tables.Count; i++)
        {
            var t = tables[i]; var headers = t.Headers.ToArray();
            var rows = new[] { headers }.Concat(t.Rows.Select(r => headers.Select(r.Get).ToArray()));
            Add($"xl/worksheets/sheet{i}.xml", new(new XElement(ns + "worksheet", new XElement(ns + "sheetData", rows.Select((row, ri) => new XElement(ns + "row", new XAttribute("r", ri + 1), row.Select((value, ci) => new XElement(ns + "c", new XAttribute("r", $"{(char)('A' + ci)}{ri + 1}"), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value))))))))));
        }
    }
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "transit-tests-" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}

public sealed class ProvidedWorkbookFactAttribute : FactAttribute
{
    public ProvidedWorkbookFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRANSIT_WORKBOOK")))
            Skip = "Set TRANSIT_WORKBOOK to run the optional provided workbook integration test.";
    }
}
