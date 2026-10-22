namespace Common.Email;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync(string key, IReadOnlyDictionary<string, string?> values, CancellationToken ct);
}
