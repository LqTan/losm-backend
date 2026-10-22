namespace AgentCore.Infrastructure.PromptsManagement;

public sealed class EmailTemplateRenderer(PromptProvider provider) : Common.Email.IEmailTemplateRenderer
{
    public Task<string> RenderAsync(string key, IReadOnlyDictionary<string, string?> values, CancellationToken ct) =>
        ManagedTemplates.RenderAsync(provider, key, ct, values.Select(v => (v.Key, (object?)v.Value)).ToArray());
}
