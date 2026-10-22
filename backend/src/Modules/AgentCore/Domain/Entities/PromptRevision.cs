namespace AgentCore.Domain.Entities;

public sealed class PromptRevision
{
    public string Key { get; set; } = null!;
    public long Version { get; set; }
    public string Content { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
    public Guid? ChangedBy { get; set; }
}
