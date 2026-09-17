using System.Security.Claims;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Agent.Commands.ApproveAgentPlan;
using AgentCore.Application.Agent.Commands.ConfirmAgentAction;
using AgentCore.Application.Agent.Commands.CreateAgentPlan;
using AgentCore.Application.Agent.Commands.ExecuteAgent;
using AgentCore.Application.Agent.Commands.RetryMeetingEmails;
using AgentCore.Application.Agent.Queries.GetAgentSessionHistory;
using AgentCore.Application.Agent.Queries.GetAgentSessionsByUser;
using AgentCore.Application.Agent.Queries.GetMeetingsForUser;
using AgentCore.Application.Agent.Queries.GetPendingActionById;
using AgentCore.Application.Agent.Queries.GetPendingActionsForUser;
using AgentCore.Infrastructure.Streaming;
using AgentCore.Presentation.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentCore.Presentation.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize]
public sealed class AgentController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Execute(
        ExecuteAgentRequest request,
        [FromServices] ExecuteAgentHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await handler.HandleAsync(
            new ExecuteAgentCommand(
                request.Message,
                request.Latitude,
                request.Longitude,
                request.SessionId,
                userId
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpPost("stream")]
    public async Task ExecuteStream(
        ExecuteAgentRequest request,
        [FromServices] IAgentRunner runner,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            Response.StatusCode = 401;
            return;
        }

        var sink = new SseAgentEventSink();

        var runnerTask = runner.RunStreamedAsync(
            request.Message,
            request.Latitude,
            request.Longitude,
            request.SessionId,
            userId,
            sink,
            cancellationToken
        );

        var writerTask = SseWriter.WriteSseStreamAsync(
            Response,
            sink,
            cancellationToken
        );

        try
        {
            await runnerTask;
        }
        catch (Exception)
        {
            // error already emitted to sink
        }
        finally
        {
            sink.Complete();
        }

        await writerTask;
    }

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan(
        CreateAgentPlanRequest request,
        [FromServices] CreateAgentPlanHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(
            new CreateAgentPlanCommand(
                request.Message,
                request.Latitude,
                request.Longitude,
                request.SessionId,
                userId
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpPost("plans/{planId:guid}/approve")]
    public async Task<IActionResult> ApprovePlan(
        Guid planId,
        [FromServices] ApproveAgentPlanHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(
            new ApproveAgentPlanCommand(
                planId,
                userId
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmAction(
        ConfirmAgentActionRequest request,
        [FromServices] ConfirmAgentActionHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(
            new ConfirmAgentActionCommand(
                request.ActionId,
                userId
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSessionHistory(
        Guid sessionId,
        [FromServices] GetAgentSessionHistoryHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(
            new GetAgentSessionHistoryQuery(
                sessionId,
                userId
            ),
            cancellationToken
        );

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions(
        [FromServices] GetAgentSessionsByUserHandler handler,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var effectiveLimit = limit is > 0 and <= 200 ? limit.Value : 50;

        var result = await handler.HandleAsync(
            new GetAgentSessionsByUserQuery(
                userId,
                effectiveLimit
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpGet("meetings")]
    public async Task<IActionResult> ListMeetings(
        [FromServices] GetMeetingsForUserHandler handler,
        [FromQuery] string? scope,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var normalizedScope = (scope ?? MeetingScope.All).ToLowerInvariant();
        if (normalizedScope != MeetingScope.All &&
            normalizedScope != MeetingScope.Upcoming &&
            normalizedScope != MeetingScope.Past)
        {
            normalizedScope = MeetingScope.All;
        }

        var effectiveLimit = limit is > 0 and <= 200 ? limit.Value : 100;

        var result = await handler.HandleAsync(
            new GetMeetingsForUserQuery(
                userId,
                normalizedScope,
                effectiveLimit
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpGet("pending-actions")]
    public async Task<IActionResult> ListPendingActions(
        [FromServices] GetPendingActionsForUserHandler handler,
        [FromQuery] int? limit,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var effectiveLimit = limit is > 0 and <= 200 ? limit.Value : 50;

        var result = await handler.HandleAsync(
            new GetPendingActionsForUserQuery(
                userId,
                effectiveLimit
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpGet("pending-actions/{actionId:guid}")]
    public async Task<IActionResult> GetPendingAction(
        Guid actionId,
        [FromServices] GetPendingActionByIdHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(
            new GetPendingActionByIdQuery(actionId, userId),
            cancellationToken
        );

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost("retry-meeting-emails/{meetingActionId:guid}")]
    public async Task<IActionResult> RetryMeetingEmails(
        Guid meetingActionId,
        [FromServices] RetryMeetingEmailsHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await handler.HandleAsync(
            new RetryMeetingEmailsCommand(meetingActionId, userId),
            cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
