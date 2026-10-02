namespace AgentCore.Application.Abstractions;

public interface IAgentExecutionContext
{
    Guid? UserId { get; }
    Guid? SessionId { get; }
    double Latitude { get; }

    double Longitude { get; }
    void SetUser(Guid? userId);

    void SetSession(Guid? sessionId);

    void SetLocation(
        double latitude,
        double longitude
    );
}
