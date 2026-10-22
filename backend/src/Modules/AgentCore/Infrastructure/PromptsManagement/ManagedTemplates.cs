using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentCore.Infrastructure.Services;

namespace AgentCore.Infrastructure.PromptsManagement;

public static class ManagedTemplates
{
    private static readonly Lazy<PromptDefinition[]> Definitions = new(() =>
        JsonSerializer.Deserialize<PromptDefinition[]>(PromptFile.Load("managed-templates.json"))
        ?? throw new InvalidOperationException("Template catalog is missing."));
    public static IReadOnlyList<PromptDefinition> All => Definitions.Value;

    public static async Task<string> RenderAsync(PromptProvider? provider, string key, CancellationToken ct,
        params (string Name, object? Value)[] values)
    {
        var definition = All.First(d => d.Key == key);
        var content = provider is null ? definition.DefaultContent
            : await provider.ResolveAsync(key, definition.DefaultContent, ct);
        return Render(definition, content, values);
    }

    public static string Render(PromptDefinition definition, string content,
        params (string Name, object? Value)[] values)
    {
        PromptCatalog.Validate(definition, content);
        var data = values.ToDictionary(v => v.Name,
            v => Convert.ToString(v.Value, CultureInfo.InvariantCulture) ?? string.Empty);
        if (definition.Variables.Any(v => !data.ContainsKey(v)))
            throw new ArgumentException("Missing template variables.");
        // Single-pass replacement: inserted user text is never interpreted as template syntax.
        return Regex.Replace(content, @"\{([A-Za-z][A-Za-z0-9_]*)\}", m =>
            definition.Key.EndsWith(".html", StringComparison.Ordinal)
                ? WebUtility.HtmlEncode(data[m.Groups[1].Value]) : data[m.Groups[1].Value]);
    }
}
