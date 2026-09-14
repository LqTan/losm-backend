using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentCore.Application.Abstractions;

public enum ToolKind
{
    ReadOnly = 0,
    WriteRequiresConfirmation = 1,
    WriteAutoApply = 2
}

public sealed record ToolDisplay(
    string DisplayName,
    string CallingMessage,
    string SuccessMessage,
    string FailedMessage,
    string PendingConfirmationMessage,
    string? ErrorHintMessage = null
)
{
    public static ToolDisplay Default(string toolName) => new(
        DisplayName: toolName,
        CallingMessage: $"Đang thực hiện {toolName}",
        SuccessMessage: $"Đã thực hiện {toolName} xong",
        FailedMessage: $"{toolName} lỗi",
        PendingConfirmationMessage: "Chờ bạn xác nhận",
        ErrorHintMessage: null
    );
}

public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    ToolKind Kind { get; }
    ToolDisplay Display { get; }
    bool SuppliesPlaces { get; }
    JsonNode ParametersSchema { get; }
    Task<string> ExecuteAsync(
        JsonElement arguments,
        CancellationToken cancellationToken = default
    );
}
