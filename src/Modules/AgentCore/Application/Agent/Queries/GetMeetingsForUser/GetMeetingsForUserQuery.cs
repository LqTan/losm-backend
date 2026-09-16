using AgentCore.Domain.Enums;

namespace AgentCore.Application.Agent.Queries.GetMeetingsForUser;

public sealed record GetMeetingsForUserQuery(
    Guid UserId,
    string Scope = "all",
    int? Limit = null
);

public static class MeetingScope
{
    public const string All = "all";
    public const string Upcoming = "upcoming";
    public const string Past = "past";
}