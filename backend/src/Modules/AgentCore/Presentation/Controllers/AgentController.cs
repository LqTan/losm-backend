using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using AgentCore.Application.Enums;
using AgentCore.Application.Models;
using AgentCore.Infrastructure.Streaming;
using AgentCore.Presentation.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;
using Search.Application.Search.Queries.SearchMeetingPlaces;

namespace AgentCore.Presentation.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize]
public sealed class AgentController : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Execute(
        ExecuteAgentRequest request,
        [FromServices] ExecuteAgentHandler handler,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        var userId = Guid.TryParse(userIdClaim, out var parsed)
            ? parsed
            : Guid.Parse("00000000-0000-0000-0000-000000000001");

        var sessionId = Guid.TryParse(request.SessionId, out var parsedSession)
            ? parsedSession
            : (Guid?)null;

        var result = await handler.HandleAsync(
            new ExecuteAgentCommand(
                request.Message,
                request.Latitude,
                request.Longitude,
                sessionId,
                userId
            ),
            cancellationToken
        );

        return Ok(result);
    }

    [HttpPost("stream")]
    [AllowAnonymous]
    public async Task ExecuteStream(
        ExecuteAgentRequest request,
        [FromServices] IAgentRunner runner,
        CancellationToken cancellationToken
    )
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        var userId = Guid.TryParse(userIdClaim, out var parsed)
            ? parsed
            : Guid.Parse("00000000-0000-0000-0000-000000000001");

        var sessionId = Guid.TryParse(request.SessionId, out var parsedSession)
            ? parsedSession
            : (Guid?)null;

        var sink = new SseAgentEventSink();

        var runnerTask = runner.RunStreamedAsync(
            request.Message,
            request.Latitude,
            request.Longitude,
            sessionId,
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

        var sessionId = Guid.TryParse(request.SessionId, out var parsedSession)
            ? parsedSession
            : (Guid?)null;

        var result = await handler.HandleAsync(
            new CreateAgentPlanCommand(
                request.Message,
                request.Latitude,
                request.Longitude,
                sessionId,
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

    [HttpPost("meeting-search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchMeetingPlacesByPrompt(
        [FromBody] MeetingSearchPromptRequest request,
        [FromServices] IAgentModelClient modelClient,
        [FromServices] IGeocodingService geocodingService,
        [FromServices] SearchMeetingPlacesHandler meetingPlacesHandler,
        [FromServices] ILogger<AgentController> logger,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Yêu cầu không được để trống.");
        }

        var systemInstruction = """
Bạn là chuyên gia bóc tách thông tin địa điểm và người tham gia họp nhóm.
Nhiệm vụ: Phân tích câu yêu cầu họp nhóm bằng tiếng Việt từ người dùng.
Hãy xác định:
1. "query": Thể loại hoặc loại hình địa điểm cần tìm (ví dụ: 'quán ăn', 'quán cà phê', 'quán lẩu', 'nhà hàng', 'trà sữa'...). Mặc định là 'quán cà phê' nếu người dùng không nói rõ.
2. "origins": Danh sách tất cả những người tham gia cùng vị trí/địa danh xuất phát của họ:
   - Đối với người nói ("tôi", "mình"):
     * Nếu có nêu địa điểm (ví dụ: "tôi đang ở Chợ Bến Thành"), đặt name = "Tôi", location = "Chợ Bến Thành".
     * Nếu không nói rõ địa điểm hoặc nói "ở vị trí của tôi", đặt name = "Tôi", location = "GPS_CURRENT".
   - Đối với những người còn lại ("bạn tôi", "bạn A", v.v.):
     * Đặt name = "Người 1", "Người 2"... (hoặc tên riêng nếu có trong câu)
     * location = tên địa danh cụ thể được nhắc tới (ví dụ: "UBND Quận 1", "Nhà thờ Đức Bà", v.v.).

Chỉ xuất ra DUY NHẤT một chuỗi JSON hợp lệ theo cấu trúc sau, không kèm bất kỳ giải thích nào khác:
{
  "query": "quán ăn",
  "origins": [
    { "name": "Tôi", "location": "Chợ Bến Thành" },
    { "name": "Người 1", "location": "UBND Quận 1" }
  ]
}
""";

        string parsedQuery = "quán cà phê";
        var parsedOrigins = new List<(string Name, string Location)>();

        try
        {
            var messages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, systemInstruction),
                new(AgentModelRole.User, request.Prompt)
            };

            var modelResp = await modelClient.SendAsync(messages, [], cancellationToken);
            var content = modelResp.AssistantMessage?.Content ?? string.Empty;

            var match = Regex.Match(content, @"\{[\s\S]*\}");
            if (match.Success)
            {
                using var doc = JsonDocument.Parse(match.Value);
                if (doc.RootElement.TryGetProperty("query", out var qElem) && qElem.ValueKind == JsonValueKind.String)
                {
                    parsedQuery = qElem.GetString() ?? "quán cà phê";
                }

                if (doc.RootElement.TryGetProperty("origins", out var originsElem) && originsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in originsElem.EnumerateArray())
                    {
                        var name = o.TryGetProperty("name", out var nElem) && nElem.ValueKind == JsonValueKind.String
                            ? nElem.GetString() ?? "Người"
                            : "Người";
                        var loc = o.TryGetProperty("location", out var lElem) && lElem.ValueKind == JsonValueKind.String
                            ? lElem.GetString() ?? ""
                            : "";
                        parsedOrigins.Add((name, loc));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse meeting prompt with MiniMax, using fallback extraction.");
        }

        // Fallback if AI returned no origins
        if (parsedOrigins.Count == 0)
        {
            parsedOrigins.Add(("Tôi", "GPS_CURRENT"));
            parsedOrigins.Add(("Người 1", "UBND Quận 1"));
        }
        else if (parsedOrigins.Count == 1)
        {
            parsedOrigins.Add(("Người 1", "UBND Quận 1"));
        }

        var myLat = request.UserLatitude ?? 10.7769;
        var myLng = request.UserLongitude ?? 106.7009;

        var resolvedOrigins = new List<MeetingSearchOriginItem>();

        for (int i = 0; i < parsedOrigins.Count; i++)
        {
            var (oName, oLoc) = parsedOrigins[i];
            var isMe = i == 0 || oName.Contains("Tôi", StringComparison.OrdinalIgnoreCase);
            var name = isMe ? "Tôi" : (string.IsNullOrWhiteSpace(oName) ? $"Người {i}" : oName);

            double lat;
            double lng;
            string address;

            if (string.Equals(oLoc, "GPS_CURRENT", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(oLoc))
            {
                lat = myLat;
                lng = myLng;
                address = "Vị trí GPS hiện tại";
            }
            else
            {
                var geocoded = await geocodingService.GeocodeAsync(oLoc, cancellationToken);
                if (geocoded is not null)
                {
                    lat = geocoded.Latitude;
                    lng = geocoded.Longitude;
                    address = geocoded.DisplayName ?? oLoc;
                }
                else
                {
                    lat = myLat + (i * 0.012);
                    lng = myLng + (i * 0.012);
                    address = oLoc;
                }
            }

            resolvedOrigins.Add(new MeetingSearchOriginItem(
                Id: isMe ? "me" : $"orig_{i}",
                Name: name,
                Address: address,
                Lat: lat,
                Lng: lng,
                AvatarUrl: null,
                IsMe: isMe
            ));
        }

        // Merge friends if any passed from user selection
        if (request.Friends is not null && request.Friends.Count > 0)
        {
            int friendIdx = resolvedOrigins.Count;
            foreach (var f in request.Friends)
            {
                if (!resolvedOrigins.Any(r => r.Name.Equals(f.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    resolvedOrigins.Add(new MeetingSearchOriginItem(
                        Id: $"friend_{friendIdx++}",
                        Name: f.Name,
                        Address: "Vị trí bạn bè",
                        Lat: f.Latitude,
                        Lng: f.Longitude,
                        AvatarUrl: f.AvatarUrl,
                        IsMe: false
                    ));
                }
            }
        }

        var queryObj = new SearchMeetingPlacesQuery(
            parsedQuery,
            resolvedOrigins.Select(o => new SearchMeetingPlaceOrigin(o.Name, o.Lat, o.Lng)).ToList(),
            RadiusKm: 6,
            TopK: 10
        );

        var searchResult = await meetingPlacesHandler.HandleAsync(queryObj, cancellationToken);

        string? insight = null;
        if (searchResult.Results.Count > 0)
        {
            var top = searchResult.Results[0];
            insight = $"Hệ thống đã tính toán điểm hẹn công bằng nhất cho nhóm {resolvedOrigins.Count} người là \"{top.Name}\" ({top.Address ?? "TP.HCM"}). " +
                      $"Khoảng cách trung bình giữa các thành viên là {top.AverageDistanceKm:F1} km với độ công bằng lộ trình đạt {Math.Round(top.FairnessScore * 100)}%.";
        }

        var resultDtos = searchResult.Results.Select(r => new MeetingPlaceResultDto(
            PlaceId: r.PlaceId.ToString(),
            Name: r.Name,
            Address: r.Address,
            Latitude: r.Latitude,
            Longitude: r.Longitude,
            Category: r.Category,
            OpeningHours: r.OpeningHours,
            DistanceKmByOrigin: r.DistanceKmByOrigin,
            AverageDistanceKm: r.AverageDistanceKm,
            MaxDistanceKm: r.MaxDistanceKm,
            SpreadKm: r.SpreadKm,
            RelevanceScore: r.RelevanceScore,
            DistanceScore: r.DistanceScore,
            FairnessScore: r.FairnessScore,
            FinalScore: r.FinalScore,
            DurationMinutesByOrigin: r.DurationMinutesByOrigin,
            AverageDurationMinutes: r.AverageDurationMinutes,
            SpreadMinutes: r.SpreadMinutes,
            IsRoadRouting: r.IsRoadRouting
        )).ToList();

        var response = new MeetingSearchPromptResponse(
            Query: parsedQuery,
            Centroid: new MeetingSearchCentroid(searchResult.CentroidLatitude, searchResult.CentroidLongitude),
            Origins: resolvedOrigins,
            Results: resultDtos,
            AiInsight: insight
        );

        return Ok(response);
    }
}
