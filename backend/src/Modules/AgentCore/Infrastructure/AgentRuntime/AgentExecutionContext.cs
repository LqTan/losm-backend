using AgentCore.Application.Abstractions;

namespace AgentCore.Infrastructure.AgentRuntime;

public sealed class AgentExecutionContext
    : IAgentExecutionContext
{
    public Guid? UserId { get; private set; }
    public Guid? SessionId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }

    public void SetUser(Guid? userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "UserId cannot be empty.",
                nameof(userId)
            );
        }
        UserId = userId;
    }

    public void SetSession(Guid? sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "SessionId cannot be empty.",
                nameof(sessionId)
            );
        }
        SessionId = sessionId;
    }

    public void SetLocation(
        double latitude,
        double longitude
    )
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude)
            );
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude)
            );
        }

        Latitude = latitude;
        Longitude = longitude;
    }
}
