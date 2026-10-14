using System.Text.Json.Serialization;

namespace AgentCore.Application.Tools.GetDirections;

public sealed record GetDirectionsToolArguments(
    [property: JsonPropertyName("origin")] string Origin,
    [property: JsonPropertyName("destination")] string Destination,
    [property: JsonPropertyName("member_name")] string? MemberName = null
);
