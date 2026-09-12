using System.Text.Json;
using AgentCore.Application.Models;

namespace AgentCore.Infrastructure.AgentRuntime;

internal static class AttachedPlaceExtractor
{
    public static IReadOnlyList<AttachedPlace> Extract(
        string toolName,
        string toolResultJson,
        IReadOnlySet<Guid> seen
    )
    {
        if (string.IsNullOrWhiteSpace(toolResultJson))
        {
            return [];
        }

        if (toolName != "search_places")
        {
            return [];
        }

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
                if (elem.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var placeId = TryGetGuid(elem, "placeId");
                if (placeId is null || seen.Contains(placeId.Value))
                {
                    continue;
                }

                var name = TryGetString(elem, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                added.Add(
                    new AttachedPlace(
                        placeId.Value,
                        name,
                        TryGetString(elem, "address"),
                        TryGetDouble(elem, "latitude") ?? 0,
                        TryGetDouble(elem, "longitude") ?? 0,
                        TryGetString(elem, "category"),
                        TryGetDouble(elem, "distanceKm"),
                        TryGetDouble(elem, "finalScore")
                    )
                );
            }
        }
        catch (JsonException)
        {
            // tool result not parseable; skip silently
        }

        return added;
    }

    private static Guid? TryGetGuid(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            var raw = prop.GetString();
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        return null;
    }

    private static string? TryGetString(
        JsonElement obj,
        string propertyName
    )
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return prop.ToString();
    }

    private static double? TryGetDouble(
        JsonElement obj,
        string propertyName
    )
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d))
        {
            return d;
        }

        return null;
    }
}
