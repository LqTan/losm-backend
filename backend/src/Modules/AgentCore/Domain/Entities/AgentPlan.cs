using AgentCore.Domain.Enums;

namespace AgentCore.Domain.Entities;

public class AgentPlan
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? SessionId { get; private set; }
    public string UserInput { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public AgentPlanStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AgentPlan(){}
    public AgentPlan(
        Guid id,
        Guid userId,
        Guid? sessionId,
        string userInput,
        string content,
        double latitude,
        double longitude
    )
    {
        Id = id;
        UserId = userId;
        SessionId = sessionId;
        UserInput = userInput;
        Content = content;
        Latitude = latitude;
        Longitude = longitude;
        Status = AgentPlanStatus.PendingApproval;
        CreatedAt = DateTime.UtcNow;
    }
    
    public void Approve()
    {
        if (Status != AgentPlanStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                "Only a pending plan can be approved."
            );
        }
        Status = AgentPlanStatus.Approved;
        ApprovedAt = DateTime.UtcNow;
    }

    public void Complete(Guid sessionId)
    {
        if (Status != AgentPlanStatus.Approved)
        {
            throw new InvalidOperationException(
                "Only an approved plan can be completed."
            );
        }

        SessionId = sessionId;
        Status = AgentPlanStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }
}
