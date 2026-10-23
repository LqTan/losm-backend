using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualBasic.FileIO;

namespace Transit.Application;

public sealed record FileTable(string Name, IReadOnlyList<FileRow> Rows, IReadOnlySet<string> Headers);
public sealed record FileRow(int Number, Dictionary<string, string> Cells)
{
    public string Get(string column) => Cells.GetValueOrDefault(column, "").Trim();
}
public static class TransitFileReader
{
    public const long MaxUploadBytes = 25 * 1024 * 1024;
    private const long MaxExpandedBytes = 100 * 1024 * 1024;
    private const int MaxRows = 100_000;
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static readonly Dictionary<string, string> TableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tuyen"] = "routes", ["Tuyen_Chieu"] = "route_patterns", ["Tram"] = "stops", ["Tuyen_Tram"] = "route_stops",
        ["routes"] = "routes", ["route_patterns"] = "route_patterns", ["stops"] = "stops", ["route_stops"] = "route_stops"
    };
    public static List<FileTable> Read(IEnumerable<string> paths, CancellationToken ct = default)
    {
        var files = paths.ToArray();
        List<FileTable> tables;
        if (files.Length == 1 && Path.GetExtension(files[0]).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            tables = ReadWorkbook(files[0], ct);
        else
        {
            if (files.Length != 4 || files.Any(f => !Path.GetExtension(f).Equals(".csv", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Chọn một XLSX hoặc đủ bốn CSV: routes, route_patterns, stops, route_stops.");
            tables = files.Select(f => ReadCsv(f, ct)).ToList();
        }
        if (tables.Select(t => t.Name).Distinct().Count() != 4 || tables.Count != 4)
            throw new InvalidDataException("Thiếu hoặc trùng bảng. Cần routes, route_patterns, stops, route_stops.");
        return tables;
    }
    private static XDocument Xml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxExpandedBytes });
        return XDocument.Load(reader);
    }
    private static List<FileTable> ReadWorkbook(string path, CancellationToken ct)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Sum(e => e.Length) > MaxExpandedBytes || zip.Entries.Count > 1000)
            throw new InvalidDataException("XLSX giải nén vượt giới hạn 100 MB hoặc quá nhiều thành phần.");
        var strings = zip.GetEntry("xl/sharedStrings.xml") is { } ss
            ? Xml(ss).Descendants(S + "si").Select(x => string.Concat(x.Descendants(S + "t").Select(t => t.Value))).ToArray() : [];
        var workbook = Xml(zip.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("Không tìm thấy workbook."));
        var relations = Xml(zip.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("Thiếu workbook relationships."))
            .Root!.Elements().Where(x => (string?)x.Attribute("TargetMode") != "External")
            .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
        var result = new List<FileTable>();
        foreach (var sheet in workbook.Descendants(S + "sheet"))
        {
            ct.ThrowIfCancellationRequested();
            var name = (string?)sheet.Attribute("name") ?? "";
            if (!TableNames.TryGetValue(name, out var canonical)) continue;
            var id = (string?)sheet.Attribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"));
            if (id is null || !relations.TryGetValue(id, out var target)) throw new InvalidDataException($"Sheet {name} không có dữ liệu.");
            var entryName = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            var doc = Xml(zip.GetEntry(entryName) ?? throw new InvalidDataException($"Không tìm thấy sheet {name}."));
            var raw = new List<(int, string[])>();
            foreach (var row in doc.Descendants(S + "sheetData").Elements(S + "row"))
            {
                ct.ThrowIfCancellationRequested();
                var cells = new SortedDictionary<int, string>();
                foreach (var c in row.Elements(S + "c"))
                {
                    var reference = (string?)c.Attribute("r") ?? throw new InvalidDataException("Ô thiếu địa chỉ.");
                    var col = 0;
                    foreach (var ch in reference.TakeWhile(char.IsLetter)) col = checked(col * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
                    if (col is < 1 or > 100) throw new InvalidDataException("Sheet vượt 100 cột.");
                    if (c.Element(S + "f") is not null || (string?)c.Attribute("t") == "e")
                        throw new InvalidDataException($"{name}!{reference}: chỉ nhận giá trị, không nhận công thức/ô lỗi.");
                    var value = (string?)c.Element(S + "v") ?? "";
                    value = (string?)c.Attribute("t") switch
                    {
                        "s" => int.TryParse(value, out var ix) && ix >= 0 && ix < strings.Length ? strings[ix] : throw new InvalidDataException("Shared string không hợp lệ."),
                        "inlineStr" => string.Concat(c.Descendants(S + "t").Select(t => t.Value)), _ => value
                    };
                    cells[col - 1] = value;
                }
                if (cells.Count == 0) continue;
                var values = new string[cells.Keys.Max() + 1];
                Array.Fill(values, "");
                foreach (var (i, v) in cells) values[i] = v;
                raw.Add(((int?)row.Attribute("r") ?? raw.Count + 1, values));
                if (raw.Count > MaxRows) throw new InvalidDataException("Mỗi bảng tối đa 100.000 dòng.");
            }
            result.Add(Table(canonical, raw));
        }
        return result;
    }
    private static FileTable ReadCsv(string path, CancellationToken ct)
    {
        if (!TableNames.TryGetValue(Path.GetFileNameWithoutExtension(path), out var name))
            throw new InvalidDataException($"Tên CSV không được hỗ trợ: {Path.GetFileName(path)}.");
        using var input = new StreamReader(path, new UTF8Encoding(false, true), true);
        var first = input.ReadLine() ?? "";
        var delimiter = first.Contains(';') && !first.Contains(',') ? ";" : ",";
        input.DiscardBufferedData(); input.BaseStream.Position = 0;
        using var parser = new TextFieldParser(input) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(delimiter);
        var raw = new List<(int, string[])>();
        while (!parser.EndOfData)
        {
            ct.ThrowIfCancellationRequested();
            var line = checked((int)parser.LineNumber);
            raw.Add((line, parser.ReadFields() ?? []));
            if (raw.Count > MaxRows) throw new InvalidDataException("Mỗi bảng tối đa 100.000 dòng.");
        }
        return Table(name, raw);
    }
    private static FileTable Table(string name, List<(int Row, string[] Values)> raw)
    {
        if (raw.Count == 0) throw new InvalidDataException($"Bảng {name} rỗng.");
        var headers = raw[0].Values.Select(x => x.Trim().Trim('\uFEFF').ToLowerInvariant()).ToArray();
        if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct().Count() != headers.Length)
            throw new InvalidDataException($"Bảng {name} có cột trống/trùng.");
        var rows = new List<FileRow>();
        foreach (var (line, values) in raw.Skip(1))
        {
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            if (values.Length > headers.Length && values.Skip(headers.Length).Any(x => !string.IsNullOrWhiteSpace(x)))
                throw new InvalidDataException($"{name}, dòng {line}: dư cột.");
            if (values.Any(x => x.Length > 4000)) throw new InvalidDataException($"{name}, dòng {line}: giá trị vượt 4.000 ký tự.");
            rows.Add(new FileRow(line, headers.Select((h, i) => (h, v: i < values.Length ? values[i] : "")).ToDictionary(x => x.h, x => x.v)));
        }
        return new FileTable(name, rows, headers.ToHashSet());
    }
}
