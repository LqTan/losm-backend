using AgentCore.Application.Abstractions;

namespace AgentCore.Infrastructure.AgentRuntime;

public static class StepSummary
{
    public static string ForToolCall(IAgentTool tool)
        => string.IsNullOrEmpty(tool.Display.CallingMessage)
            ? $"Đang thực hiện {tool.Name}"
            : tool.Display.CallingMessage;

    public static string ForToolResult(IAgentTool tool, bool succeeded)
        => succeeded
            ? (string.IsNullOrEmpty(tool.Display.SuccessMessage)
                ? $"Đã thực hiện {tool.Name} xong"
                : tool.Display.SuccessMessage)
            : (string.IsNullOrEmpty(tool.Display.FailedMessage)
                ? $"{tool.Name} lỗi"
                : tool.Display.FailedMessage);

    public static string ForPendingAction(IAgentTool tool)
        => string.IsNullOrEmpty(tool.Display.PendingConfirmationMessage)
            ? "Chờ bạn xác nhận"
            : tool.Display.PendingConfirmationMessage;

    public const string Thinking = "Đang suy nghĩ";
    public const string Finalize = "Đã soạn xong câu trả lời";
    public const string FinalizeFailed = "Tạo câu trả lời lỗi";
}
