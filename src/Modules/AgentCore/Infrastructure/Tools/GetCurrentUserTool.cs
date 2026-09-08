using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.CurrentUser;
using Users.Application.Users.Queries.GetUserById;

namespace AgentCore.Infrastructure.Tools;

public sealed class GetCurrentUserTool
    : AgentTool<GetCurrentUserToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly GetUserByIdHandler _getUserByIdHandler;
    private readonly IAgentExecutionContext _executionContext;

    public GetCurrentUserTool(
        GetUserByIdHandler getUserByIdHandler,
        IAgentExecutionContext executionContext
    )
    {
        _getUserByIdHandler = getUserByIdHandler;
        _executionContext = executionContext;
    }

    public override string Name => "get_current_user";

    public override string Description =>
        "Get basic profile information for the currently authenticated user.";

    protected override async Task<string> ExecuteAsync(
        GetCurrentUserToolArguments arguments,
        CancellationToken cancellationToken
    )
    {
        var userId = _executionContext.UserId;

        if (!userId.HasValue)
        {
            throw new InvalidOperationException(
                "Current user is not available."
            );
        }

        var user = await _getUserByIdHandler.HandleAsync(
            new GetUserByIdQuery(
                userId.Value
            )
        );

        if (user is null)
        {
            throw new KeyNotFoundException(
                $"User '{userId.Value}' was not found."
            );
        }

        var result = new
        {
            user.Id,
            user.Username,
            user.CreatedAt
        };

        return JsonSerializer.Serialize(
            result,
            JsonOptions
        );
    }
}
