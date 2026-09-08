using System.Text;
using Agent.Application.Abstractions;
using Agent.Domain;

namespace Agent.Application.Prompts;

public sealed class PlannerPromptBuilder : IPromptBuilder
{
    public string BuildPlannerPrompt(string userQuery, AgentPlan? previousPlan = null)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("Bạn là Planner cho hệ thống tìm kiếm và gợi ý địa điểm dựa trên vị trí.");
        prompt.AppendLine("Nhiệm vụ của bạn là phân tích yêu cầu người dùng và trả về đúng một JSON hợp lệ.");
        prompt.AppendLine();
        prompt.AppendLine("Nguyên tắc bắt buộc:");
        prompt.AppendLine("- Không bịa địa điểm, tọa độ GPS, giá, rating, review hoặc giờ mở cửa.");
        prompt.AppendLine("- Không tạo latitude hoặc longitude. GPS do ứng dụng gửi riêng.");
        prompt.AppendLine("- Nếu người dùng nói “gần tôi”, “quanh đây” hoặc “near me”, đặt requiresLocation là true.");
        prompt.AppendLine("- Nếu không thể xác định truy vấn để tìm địa điểm, đặt needsClarification là true và hỏi một câu ngắn.");
        prompt.AppendLine("- searchQuery là cụm từ ngắn có thể gửi vào dịch vụ tìm địa điểm; bỏ các cụm chỉ vị trí như “gần tôi”.");
        prompt.AppendLine("- radiusKm là bán kính theo km, chỉ chọn giá trị từ 0.5 đến 20; dùng 5 nếu người dùng không nêu rõ.");
        prompt.AppendLine("- shouldLoadReviews là true khi review/rating giúp trả lời yêu cầu.");
        prompt.AppendLine("- shouldUseSemanticRerank là true khi truy vấn có ngữ cảnh, tiêu chí hoặc preference; có thể false với query rất đơn giản.");
        prompt.AppendLine("- Chỉ trả về JSON, không viết Markdown hoặc giải thích bên ngoài JSON.");
        prompt.AppendLine();

        prompt.AppendLine("JSON phải theo cấu trúc sau:");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"intent\": \"search_place\",");
        prompt.AppendLine("  \"placeType\": \"cafe | restaurant | library | hotel | bar | null\",");
        prompt.AppendLine("  \"preferences\": [\"quiet\", \"nice_view\", \"study_friendly\", \"family_friendly\"],");
        prompt.AppendLine("  \"searchQuery\": \"string\",");
        prompt.AppendLine("  \"radiusKm\": 5,");
        prompt.AppendLine("  \"requiresLocation\": true,");
        prompt.AppendLine("  \"shouldLoadReviews\": true,");
        prompt.AppendLine("  \"shouldUseSemanticRerank\": true,");
        prompt.AppendLine("  \"needsClarification\": false,");
        prompt.AppendLine("  \"clarificationQuestion\": null");
        prompt.AppendLine("}");
        prompt.AppendLine();

        if (previousPlan is not null)
        {
            prompt.AppendLine("Kế hoạch của lượt trước:");
            prompt.AppendLine($"- Query tìm kiếm: {previousPlan.SearchQuery}");
            prompt.AppendLine($"- Loại địa điểm: {previousPlan.PlaceType ?? "chưa xác định"}");
            prompt.AppendLine($"- Tiêu chí: {string.Join(", ", previousPlan.Preferences)}");
            prompt.AppendLine($"- Bán kính: {previousPlan.RadiusKm} km");
            prompt.AppendLine();
        }

        prompt.AppendLine("Yêu cầu người dùng hiện tại:");
        prompt.AppendLine(userQuery);

        return prompt.ToString();
    }
}