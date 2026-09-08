using Agent.Application.Abstractions;
using Agent.Domain;

namespace Agent.Application.Validation;

public sealed class PlanValidator : IPlanValidator
{
    private const double MinimumRadiusKm = 0.1;
    private const double MaximumRadiusKm = 50;

    public ValidationDecision Validate(
        AgentPlan plan,
        AgentAction action,
        bool hasUserLocation)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(plan.OriginalQuery))
        {
            return ValidationDecision.Reject(
                "Yêu cầu tìm kiếm không được để trống.");
        }

        if (plan.NeedsClarification)
        {
            return ValidationDecision.Reject(
                plan.ClarificationQuestion ??
                "Cần thêm thông tin trước khi tiếp tục.");
        }

        if (action == AgentAction.SearchPlace &&
            string.IsNullOrWhiteSpace(plan.SearchQuery))
        {
            return ValidationDecision.Reject(
                "Không thể tạo truy vấn tìm kiếm từ yêu cầu hiện tại.");
        }

        if (action == AgentAction.SearchPlace &&
            (plan.RadiusKm < MinimumRadiusKm || plan.RadiusKm > MaximumRadiusKm))
        {
            return ValidationDecision.Reject(
                $"Bán kính tìm kiếm phải nằm trong khoảng từ {MinimumRadiusKm} đến {MaximumRadiusKm} km.");
        }

        if (plan.RequiresLocation && !hasUserLocation)
        {
            return ValidationDecision.Reject(
                "Cần vị trí GPS hoặc khu vực tìm kiếm trước khi tiếp tục.");
        }

        return action switch
        {
            AgentAction.SavePreference =>
                ValidationDecision.RequireHumanApproval(
                    "Bạn có muốn lưu sở thích này không?"),

            AgentAction.CreateReview =>
                ValidationDecision.RequireHumanApproval(
                    "Bạn có muốn đăng review này không?"),

            AgentAction.SearchPlace => ValidationDecision.Allow(),

            _ => ValidationDecision.Reject(
                "Hành động Agent không được hỗ trợ.")
        };
    }
}