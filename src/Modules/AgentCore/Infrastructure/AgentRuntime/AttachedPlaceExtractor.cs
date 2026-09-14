using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Models;

namespace AgentCore.Infrastructure.AgentRuntime;

internal static class AttachedPlaceExtractor
{
    public static IReadOnlyList<AttachedPlace> Extract(
        IAgentTool tool,
        string toolResultJson,
        IReadOnlySet<Guid> seen)
    {
        if (!tool.SuppliesPlaces) return [];
        if (string.IsNullOrWhiteSpace(toolResultJson)) return [];

        var added = new List<AttachedPlace>();

        try
        {
            using var doc = JsonDocument.Parse(toolResultJson);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            foreach (var elem in doc.RootElement.EnumerateArray())
            {
                if (elem.ValueKind != JsonValueKind.Object) continue;

                var placeId = TryGetGuid(elem, "placeId");
                if (placeId is null || seen.Contains(placeId.Value)) continue;

                var name = TryGetString(elem, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                added.Add(new AttachedPlace(
                    placeId.Value,
                    name,
                    TryGetString(elem, "address"),
                    TryGetDouble(elem, "latitude") ?? 0,
                    TryGetDouble(elem, "longitude") ?? 0,
                    TryGetString(elem, "category"),
                    TryGetDouble(elem, "distanceKm"),
                    TryGetDouble(elem, "finalScore")
                ));
            }
        }
        catch (JsonException)
        {
        }

        return added;
    }

    private static Guid? TryGetGuid(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;
        var raw = prop.GetString();
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private static string? TryGetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop)) return null;
        if (prop.ValueKind == JsonValueKind.Null) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static double? TryGetDouble(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d) ? d : null;
    }
}
