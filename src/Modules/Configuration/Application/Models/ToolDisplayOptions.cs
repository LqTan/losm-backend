namespace Configuration.Application.Models;

public sealed record ToolDisplayOptions
{
    public string DisplayName { get; init; } = string.Empty;
    public string CallingMessage { get; init; } = string.Empty;
    public string SuccessMessage { get; init; } = string.Empty;
    public string FailedMessage { get; init; } = string.Empty;
    public string PendingConfirmationMessage { get; init; } = string.Empty;
    public string? ErrorHintMessage { get; init; }
}
