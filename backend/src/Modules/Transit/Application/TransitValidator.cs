using System.Globalization;
using System.Text.Json;
using Transit.Infrastructure;

namespace Transit.Application;

public sealed class TransitSnapshot
{
    public List<TransitRoute> Routes { get; set; } = [];
    public List<TransitRoutePattern> Patterns { get; set; } = [];
    public List<TransitStop> Stops { get; set; } = [];
    public List<TransitRouteStop> RouteStops { get; set; } = [];
    public List<TransitServiceWindow> ServiceWindows { get; set; } = [];
    public List<TransitFareRule> FareRules { get; set; } = [];
    public List<TransitImportError> Issues { get; set; } = [];
}
public sealed class TransitValidator
{
    public TransitSnapshot Validate(List<FileTable> tables, Guid batchId, string mode, CancellationToken ct = default)
    {
        var output = new TransitSnapshot();
        var map = tables.ToDictionary(t => t.Name);
        var required = new Dictionary<string, string[]>
        {
            ["routes"] = ["route_no", "route_name"],
            ["route_patterns"] = ["route_no", "direction", "direction_name"],
            ["stops"] = ["stop_id", "stop_name", "lat", "lon"],
            ["route_stops"] = ["route_no", "direction", "seq", "stop_id"]
        };
        void Issue(string sheet, int row, string col, string message) => output.Issues.Add(new()
        { BatchId = batchId, Sheet = sheet, Row = row, Column = col, Message = message, Severity = "Error" });
        foreach (var (name, columns) in required)
        {
            if (!map.TryGetValue(name, out var table)) { Issue(name, 1, "", "Thiếu bảng."); continue; }
            foreach (var col in columns.Where(c => !table.Headers.Contains(c))) Issue(name, 1, col, "Thiếu cột bắt buộc.");
            if (table.Rows.Count == 0) Issue(name, 2, "", "Bảng phải có dữ liệu.");
        }
        if (output.Issues.Count > 0) return output;
        var routes = new Dictionary<int, TransitRoute>();
        var patterns = new Dictionary<(int, int, string), TransitRoutePattern>();
        var stops = new Dictionary<string, TransitStop>(StringComparer.OrdinalIgnoreCase);
        void Read(string table, Action<FileRow> action)
        {
            foreach (var row in map[table].Rows)
            {
                ct.ThrowIfCancellationRequested();
                try { action(row); }
                catch (CellException ex) { Issue(table, row.Number, ex.Column, ex.Message); }
            }
        }
        Read("routes", row =>
        {
            var number = Integer(row, "route_no", 1);
            var route = new TransitRoute { BatchId = batchId, Number = number, Name = Text(row, "route_name", 500, true),
                Source = Text(row, "source", 500), CollectedDate = Text(row, "collected_date", 200), ExternalId = Text(row, "route_id_site", 200) };
            var fare = Decimal(row, "fare");
            var description = Text(row, "tickets_text", 4000);
            if (!routes.TryAdd(number, route)) throw new CellException("route_no", $"Trùng mã tuyến sau chuẩn hóa thành số {number}.");
            output.Routes.Add(route);
            output.FareRules.Add(new() { BatchId = batchId, RouteId = route.Id, ReferenceAmount = fare, Description = description });
        });
        Read("route_patterns", row =>
        {
            var route = Integer(row, "route_no", 1);
            var direction = Integer(row, "direction", 0);
            if (direction > 1) throw new CellException("direction", "Chiều chạy phải là 0 hoặc 1; dùng pattern_code cho nhánh khác.");
            if (!routes.TryGetValue(route, out var parent)) throw new CellException("route_no", "Tuyến không tồn tại hoặc không hợp lệ.");
            var code = PatternCode(row);
            var first = Minute(row, "first_trip"); var last = Minute(row, "last_trip");
            var min = OptionalInt(row, "headway_min"); var max = OptionalInt(row, "headway_max");
            if (min > max) throw new CellException("headway_min", "Giãn chuyến nhỏ nhất lớn hơn lớn nhất.");
            var pattern = new TransitRoutePattern { BatchId = batchId, RouteId = parent.Id, Direction = direction, PatternCode = code,
                Name = Text(row, "direction_name", 500, true), ExternalId = Text(row, "route_var_id", 200) };
            var days = Text(row, "service_days", 200);
            if (days.Length > 0 && days != "Daily" && days != "Weekdays" && days != "Weekends" && days != "Unknown")
                throw new CellException("service_days", "Giá trị hỗ trợ: Daily, Weekdays, Weekends, Unknown.");
            if (!patterns.TryAdd((route, direction, code), pattern)) throw new CellException("pattern_code", "Trùng tuyến, chiều và biến thể hành trình.");
            output.Patterns.Add(pattern);
            output.ServiceWindows.Add(new() { BatchId = batchId, PatternId = pattern.Id, FirstMinute = first, LastMinute = last,
                HeadwayMin = min, HeadwayMax = max, ServiceDays = days.Length == 0 ? "Unknown" : days });
        });
        Read("stops", row =>
        {
            var id = Text(row, "stop_id", 200, true);
            var lat = Coordinate(row, "lat", -90, 90); var lon = Coordinate(row, "lon", -180, 180);
            if (lat == 0 && lon == 0) throw new CellException("lat", "Tọa độ (0,0) không được chấp nhận làm vị trí thay thế.");
            var kind = Text(row, "stop_kind", 200);
            if (kind.Length == 0) kind = mode == "Metro" ? "Station" : "Stop";
            if (kind is not ("Stop" or "Station" or "Platform" or "Entrance")) throw new CellException("stop_kind", "Loại trạm: Stop, Station, Platform hoặc Entrance.");
            var parent = Text(row, "parent_stop_id", 200);
            var stop = new TransitStop { BatchId = batchId, ExternalId = id, Name = Text(row, "stop_name", 500, true),
                Latitude = lat, Longitude = lon, Address = Text(row, "address", 1000), Code = Text(row, "stop_code", 200),
                Kind = kind, ParentExternalId = parent.Length == 0 ? null : parent };
            if (!stops.TryAdd(id, stop)) throw new CellException("stop_id", "Trùng mã trạm (không phân biệt hoa/thường).");
            output.Stops.Add(stop);
        });
        foreach (var row in map["stops"].Rows)
        {
            if (!stops.TryGetValue(row.Get("stop_id"), out var stop)) continue;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { stop.ExternalId };
            var parent = stop.ParentExternalId;
            while (parent is not null)
            {
                if (!visited.Add(parent)) { Issue("stops", row.Number, "parent_stop_id", "Quan hệ trạm cha có vòng lặp."); break; }
                if (!stops.TryGetValue(parent, out var next)) { Issue("stops", row.Number, "parent_stop_id", "Trạm cha không tồn tại."); break; }
                parent = next.ParentExternalId;
            }
        }
        var sequences = new HashSet<(Guid, int)>();
        Read("route_stops", row =>
        {
            var route = Integer(row, "route_no", 1); var direction = Integer(row, "direction", 0); var seq = Integer(row, "seq", 1);
            if (!patterns.TryGetValue((route, direction, PatternCode(row)), out var pattern)) throw new CellException("route_no", "Không tìm thấy tuyến/chiều/biến thể.");
            if (!stops.TryGetValue(row.Get("stop_id"), out var stop)) throw new CellException("stop_id", "Trạm không tồn tại hoặc không hợp lệ.");
            if (!sequences.Add((pattern.Id, seq))) throw new CellException("seq", "Trùng thứ tự trong hành trình.");
            output.RouteStops.Add(new() { BatchId = batchId, PatternId = pattern.Id, StopId = stop.Id, Sequence = seq });
        });
        var counts = output.RouteStops.GroupBy(x => x.PatternId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sequence).ToArray());
        foreach (var pattern in output.Patterns)
        {
            if (!counts.TryGetValue(pattern.Id, out var points) || points.Length < 2)
                Issue("route_stops", 0, "seq", $"Hành trình {routes.Values.Single(r => r.Id == pattern.RouteId).Number}/{pattern.Direction}/{pattern.PatternCode} cần ít nhất 2 trạm.");
            else if (!points.Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, points.Length)))
                Issue("route_stops", 0, "seq", $"Hành trình {routes.Values.Single(r => r.Id == pattern.RouteId).Number}/{pattern.Direction}/{pattern.PatternCode}: thứ tự phải liên tục từ 1.");
        }
        foreach (var route in output.Routes.Where(r => output.Patterns.All(p => p.RouteId != r.Id)))
            Issue("routes", 0, "route_no", $"Tuyến {route.Number} chưa có hành trình.");
        return output;
    }
    private static string PatternCode(FileRow r) => string.IsNullOrWhiteSpace(r.Get("pattern_code")) ? "default" : Text(r, "pattern_code", 200).ToLowerInvariant();
    private static string Text(FileRow r, string col, int max, bool required = false)
    {
        var s = r.Get(col);
        if (required && s.Length == 0) throw new CellException(col, "Giá trị bắt buộc.");
        if (s.Length > max) throw new CellException(col, $"Tối đa {max} ký tự.");
        return s;
    }
    private static int Integer(FileRow r, string col, int min)
    {
        if (!int.TryParse(r.Get(col), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < min)
            throw new CellException(col, $"Phải là số nguyên từ {min}; mã có chữ không được tự bỏ chữ để chuyển số.");
        return value;
    }
    private static int? OptionalInt(FileRow r, string col) => r.Get(col).Length == 0 ? null : Integer(r, col, 1);
    private static decimal? Decimal(FileRow r, string col)
    {
        if (r.Get(col).Length == 0) return null;
        if (!decimal.TryParse(r.Get(col), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value < 0 || value > 9999999999999999.99m)
            throw new CellException(col, "Giá phải là số không âm, không có dấu phân cách hàng nghìn.");
        return value;
    }
    private static double Coordinate(FileRow r, string col, double min, double max)
    {
        if (!double.TryParse(r.Get(col), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value) || value < min || value > max)
            throw new CellException(col, "Tọa độ không hợp lệ; dùng dấu chấm thập phân.");
        return value;
    }
    private static int? Minute(FileRow r, string col)
    {
        var s = r.Get(col);
        if (s.Length == 0) return null;
        if (TimeOnly.TryParseExact(s, ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return time.Hour * 60 + time.Minute;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && double.IsFinite(serial) && serial >= 0 && serial < 1)
            return Math.Min(1439, (int)Math.Round(serial * 1440));
        throw new CellException(col, "Giờ phải có dạng HH:mm hoặc ô thời gian Excel.");
    }
    private sealed class CellException(string column, string message) : Exception(message) { public string Column { get; } = column; }
}
