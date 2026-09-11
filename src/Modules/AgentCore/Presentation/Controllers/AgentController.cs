using System.Security.Claims;
using AgentCore.Application.Agent.Commands.ApproveAgentPlan;
using AgentCore.Application.Agent.Commands.ConfirmAgentAction;
using AgentCore.Application.Agent.Commands.CreateAgentPlan;
using AgentCore.Application.Agent.Commands.ExecuteAgent;
using AgentCore.Application.Agent.Queries.GetAgentSessionHistory;
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

        return Ok(result);
    }
}
