using AgentCore.Application.Abstractions;

namespace AgentCore.Infrastructure.AgentRuntime;

public static class StepSummary
{
    public static string ForToolCall(IAgentTool tool)
        => string.IsNullOrEmpty(tool.Display.CallingMessage)
            ? $"Running {tool.Name}"
            : tool.Display.CallingMessage;

    public static string ForToolResult(IAgentTool tool, bool succeeded)
        => succeeded
            ? (string.IsNullOrEmpty(tool.Display.SuccessMessage)
                ? $"Completed {tool.Name}"
                : tool.Display.SuccessMessage)
            : (string.IsNullOrEmpty(tool.Display.FailedMessage)
                ? $"{tool.Name} failed"
                : tool.Display.FailedMessage);

    public static string ForPendingAction(IAgentTool tool)
        => string.IsNullOrEmpty(tool.Display.PendingConfirmationMessage)
            ? "Awaiting confirmation"
            : tool.Display.PendingConfirmationMessage;

    public const string Thinking = "Thinking";
    public const string Finalize = "Response ready";
    public const string FinalizeFailed = "Failed to generate response";
}
