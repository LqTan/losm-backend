namespace AgentCore.Infrastructure.Llm;

public sealed class MiniMaxAgentClientOptions
{
    public string BaseUrl { get; set; } = "https://api.minimax.io/v1/";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "Minimax-M3";
    public int TimeoutSeconds { get; set; } = 60;
}
