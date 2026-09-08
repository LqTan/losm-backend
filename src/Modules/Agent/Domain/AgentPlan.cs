namespace Agent.Domain;

public sealed class AgentPlan
{
    // Giữ nguyên câu user nhập, dùng cho log, memory và giải thích kết quả.
    public string OriginalQuery { get; init; } = string.Empty;

    // Ý định ở mức workflow. Hiện use case chính là tìm địa điểm.
    public string Intent { get; init; } = "search_place";

    // Loại địa điểm được Planner nhận ra, ví dụ: cafe, restaurant, hotel.
    public string? PlaceType { get; init; }

    // Điều kiện/ý thích rút ra từ truy vấn, ví dụ: quiet, cheap, romantic.
    public List<string> Preferences { get; init; } = [];

    // Query chuẩn bị gửi sang pipeline Search/Places.
    // Nếu Planner chưa chuẩn hóa riêng, dùng OriginalQuery làm fallback.
    public string SearchQuery { get; init; } = string.Empty;

    // GPS sẽ được API/Orchestrator gắn vào trước khi gọi Search.
    // Nullable vì Planner có thể chạy trước khi client cấp location.
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    // Bán kính tìm kiếm dự kiến, đơn vị km.
    public double RadiusKm { get; init; } = 5;

    // Search gần user cần GPS; Validator dùng field này để yêu cầu location khi thiếu.
    public bool RequiresLocation { get; init; }

    // Orchestrator có thể dùng để quyết định có cần lấy review/rating chi tiết không.
    public bool ShouldLoadReviews { get; init; } = true;

    // Chỉ là yêu cầu rerank; Agent không gọi XLM-R trực tiếp.
    // Orchestrator phải fallback sang ranking hiện có nếu inference service chưa sẵn sàng.
    public bool ShouldUseSemanticRerank { get; init; } = true;

    // Planner không đủ thông tin để tạo search plan đáng tin cậy.
    public bool NeedsClarification { get; init; }

    public string? ClarificationQuestion { get; init; }
}