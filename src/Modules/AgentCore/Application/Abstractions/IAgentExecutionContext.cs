namespace AgentCore.Application.Abstractions;

public interface IAgentExecutionContext
{
    Guid? UserId { get; }
    double Latitude { get; }

    double Longitude { get; }
    void SetUser(Guid? userId);

    void SetLocation(
        double latitude,
        double longitude
    );
}
