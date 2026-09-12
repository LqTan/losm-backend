using AgentCore.Application.Models;

namespace AgentCore.Infrastructure.AgentRuntime;

internal static class StepSummary
{
    public static string FromStep(
        AgentActivityStepKind kind,
        string? toolName,
        bool succeeded
    )
    {
        return kind switch
        {
            AgentActivityStepKind.ToolCall =>
                ToolCallMessage(toolName),

            AgentActivityStepKind.ToolResult =>
                ToolResultMessage(toolName, succeeded),

            AgentActivityStepKind.PendingAction =>
                "Chờ bạn xác nhận",

            AgentActivityStepKind.Finalize =>
                succeeded
                    ? "Đã soạn xong câu trả lời"
                    : "Tạo câu trả lời lỗi",

            AgentActivityStepKind.ModelResponse =>
                "Đang suy nghĩ",

            _ =>
                "Đang xử lý"
        };
    }

    private static string ToolCallMessage(string? toolName)
    {
        return toolName switch
        {
            "search_places" =>
                "Đang tìm địa điểm gần",

            "get_place_reviews" =>
                "Đang đọc đánh giá",

            "get_current_user" =>
                "Đang xác minh tài khoản",

            "save_place" =>
                "Đang chuẩn bị lưu địa điểm",

            "create_review" =>
                "Đang soạn đánh giá",

            null or "" =>
                "Đang xử lý",

            _ =>
                $"Đang thực hiện {PrettyName(toolName)}"
        };
    }

    private static string ToolResultMessage(string? toolName, bool succeeded)
    {
        if (!succeeded)
        {
            return toolName switch
            {
                "search_places" =>
                    "Tìm địa điểm lỗi",

                "get_place_reviews" =>
                    "Đọc đánh giá lỗi",

                "get_current_user" =>
                    "Xác minh tài khoản lỗi",

                "save_place" =>
                    "Chuẩn bị lưu lỗi",

                "create_review" =>
                    "Soạn đánh giá lỗi",

                null or "" =>
                    "Xử lý lỗi",

                _ =>
                    $"{PrettyName(toolName)} lỗi"
            };
        }

        return toolName switch
        {
            "search_places" =>
                "Đã tìm xong",

            "get_place_reviews" =>
                "Đã đọc đánh giá xong",

            "get_current_user" =>
                "Đã xác minh xong",

            "save_place" =>
                "Chờ bạn xác nhận lưu",

            "create_review" =>
                "Chờ bạn xác nhận đánh giá",

            null or "" =>
                "Đã xong",

            _ =>
                $"Đã {PrettyName(toolName)} xong"
        };
    }

    private static string PrettyName(string toolName)
    {
        var spaced = toolName.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
