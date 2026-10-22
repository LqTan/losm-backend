using AgentCore.Application.Abstractions;
using AgentCore.Infrastructure.Services;
using System.Text.RegularExpressions;

namespace AgentCore.Infrastructure.PromptsManagement;

public sealed record PromptDefinition(
    string Key,
    string Group,
    string DefaultContent,
    string[] Variables,
    Dictionary<string, string>? SampleVariables = null);

public sealed class PromptCatalog(IEnumerable<IAgentTool> tools)
{
    public IReadOnlyList<PromptDefinition> GetAll() => new[]
    {
        File("agent-system"), File("planner-system"),
        File("approved-plan-system", "approvedPlan"), File("grounding-system"),
        File("area-context-system", "areasDesc")
    }.Concat(ManagedTemplates.All).Concat(tools.Select(t => new PromptDefinition("tool." + t.Name, "Tool", t.Description, []))).ToArray();

    public PromptDefinition Get(string key) => GetAll().FirstOrDefault(p => p.Key == key)
        ?? throw new KeyNotFoundException("Prompt not found.");

    private static PromptDefinition File(string key, params string[] variables) =>
        new(key, "System", PromptFile.Load(key + ".txt"), variables);

    public static void Validate(PromptDefinition definition, string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 30000)
            throw new ArgumentException("Prompt content is required and must not exceed 30,000 characters.");
        var variables = Regex.Matches(content, @"\{([A-Za-z][A-Za-z0-9_]*)\}")
            .Select(m => m.Groups[1].Value).ToHashSet();
        if (!variables.SetEquals(definition.Variables))
            throw new ArgumentException("Required variables: " + string.Join(", ", definition.Variables.Select(v => "{" + v + "}")));
    }
}
