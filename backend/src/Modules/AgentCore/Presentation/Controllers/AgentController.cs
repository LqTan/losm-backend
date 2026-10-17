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
        [FromServices] Areas.Application.Abstractions.IAreaRepository areaRepo,
        [FromServices] SearchMeetingPlacesHandler meetingPlacesHandler,
        [FromServices] Search.Application.Abstractions.IOsrmRouteService routeService,
        [FromServices] ILogger<AgentController> logger,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Yêu cầu không được để trống.");
        }

        var allAreas = await areaRepo.GetAllAsync(cancellationToken);
        var activeAreaNames = allAreas.Where(a => a.IsActive).Select(a => a.Name).ToList();
        if (activeAreaNames.Count == 0)
        {
            activeAreaNames.Add("Quận 1");
        }
        var areasDescription = string.Join(", ", activeAreaNames);

        var systemInstruction = $$"""
Bạn là trợ lý NLU/NLP (Natural Language Understanding) thông minh của nền tảng bản đồ LOSM.
Cơ sở dữ liệu hệ thống hiện tại CHỈ MỚI CÓ dữ liệu địa điểm tại các khu vực được kích hoạt: {{areasDescription}}.

Nhiệm vụ: Phân tích toàn diện câu yêu cầu bằng ngôn ngữ tự nhiên của người dùng, xác định rõ: Ý định (Intent), Chủ thể & Số lượng người (Party Size), Phân loại vị trí (Location Type), Địa danh/Mốc (Search Anchor), và Điểm xuất phát (Origins).

QUY TẮC PHÂN TÍCH & TRÍCH XUẤT CẤU TRÚC JSON:
1. "intent":
   - "directions": khi người dùng muốn tìm đường đi, chỉ đường, hỏi cách di chuyển từ đâu tới đâu.
   - "search": khi người dùng muốn tìm kiếm địa điểm, quán ăn, cà phê, điểm hẹn, hoặc gợi ý nơi chốn.

2. "party_size": Số lượng người tham gia được xác định từ ngữ cảnh:
   - 1: nếu là cá nhân đi một mình, hoặc không nói số lượng cụ thể (ví dụ: "tìm giúp tôi quán bún bò", "quán cà phê gần tôi").
   - 2, 3, 4...: nếu người dùng nói rõ số lượng người hoặc đếm được các thành viên (ví dụ: "nhóm 3 người", "Tôi và bạn tôi", "Tôi ở A, Nam ở B").

3. "location_type": BẮT BUỘC chọn chính xác 1 trong 4 loại sau:
   - "landmark": Người dùng muốn tìm kiếm xung quanh một MỐC ĐỊA DANH / CÔNG TRÌNH cụ thể (ví dụ: "gần Nhà thờ Đức Bà", "quanh Bitexco", "gần Chợ Bến Thành", "quanh phố đi bộ Nguyễn Huệ").
   - "current_location": Người dùng muốn tìm kiếm xung quanh VỊ TRÍ HIỆN TẠI CỦA CHÍNH HỌ (ví dụ: "gần tôi", "quanh đây", "chỗ tôi", "vị trí của tôi", "gần chỗ tôi đang đứng", "ở đây có quán nào...").
   - "custom_origins": Người dùng cung cấp ĐIỂM XUẤT PHÁT CỤ THỂ của bản thân hoặc từng thành viên trong nhóm (ví dụ: "Tôi đang ở Chợ Bến Thành", "Tôi ở Bến Thành, Nam ở Landmark 81").
   - "unspecified": Người dùng HOÀN TOÀN KHÔNG CUNG CẤP bất kỳ thông tin vị trí, mốc địa danh hay điểm xuất phát nào (ví dụ: "tìm giúp tôi quán bún bò", "tìm quán cafe yên tĩnh", "chỉ đường giúp tôi", "chỗ nào ăn trưa ngon").

4. "search_anchor":
   - Nếu location_type là "landmark": Trích xuất chính xác tên mốc địa danh (ví dụ: "Nhà thờ Đức Bà", "Tòa nhà Bitexco", "Chợ Bến Thành").
   - Nếu không phải "landmark", để chuỗi rỗng "".

5. "origins": Danh sách điểm xuất phát:
   - Nếu location_type là "current_location": Trả về [{"name": "Tôi", "place_name": "GPS_CURRENT", "role": "speaker"}].
   - Nếu location_type là "custom_origins": Trả về danh sách từng người kèm địa điểm xuất phát (ví dụ: [{"name": "Tôi", "place_name": "Chợ Bến Thành", "role": "speaker"}, {"name": "Nam", "place_name": "Landmark 81", "role": "member"}]).
   - Nếu location_type là "landmark" hoặc "unspecified": TUYỆT ĐỐI ĐỂ RỖNG [] (Không tự tiện thêm "Tôi" hay bất kỳ ai).

6. "needs_clarification": true hoặc false.
   - BẮT BUỘC đặt true khi location_type là "unspecified" (vì hệ thống không thể biết người dùng muốn tìm ở đâu nếu không có vị trí hay mốc địa danh).
   - BẮT BUỘC đặt true khi intent là "directions" nhưng thiếu điểm xuất phát hoặc điểm đến.
   - Đặt false khi location_type là "landmark", "current_location", hoặc "custom_origins".

7. "clarification_question":
   - Khi needs_clarification là true: Đặt câu hỏi ngắn gọn, lịch sự (1 câu) hỏi người dùng muốn tìm ở khu vực nào (ví dụ: {{areasDescription}}) hoặc gần địa danh nào.
   - Khi needs_clarification là false: Để chuỗi rỗng "".

8. "query": Thể loại địa điểm hoặc tên địa điểm người dùng muốn tìm (ví dụ: 'quán cà phê', 'quán bún bò', 'công viên'...). Mặc định 'quán cà phê' nếu không rõ.

9. "target_category": Chọn 1 trong 13 nhóm ("cafe", "viet", "japanese", "korean", "thai", "seafood", "western", "vegetarian", "chinese", "bbq_hotpot", "bookstore", "library", "park") hoặc "other".

10. "specific_preference": Sở thích cụ thể về món ăn/thương hiệu con (ví dụ: "bún bò", "ramen", "dim sum", "pizza", "highlands"...). Nếu không có, trả về "".

11. "destination": Tên địa điểm đến (chỉ dùng khi intent là "directions").

Chỉ xuất ra DUY NHẤT một chuỗi JSON hợp lệ theo cấu trúc:
{
  "intent": "search",
  "party_size": 1,
  "location_type": "landmark",
  "search_anchor": "Nhà thờ Đức Bà",
  "origins": [],
  "needs_clarification": false,
  "clarification_question": "",
  "query": "quán bún bò",
  "target_category": "viet",
  "specific_preference": "bún bò",
  "destination": ""
}
""";

        string parsedIntent = "search";
        string parsedQuery = "quán cà phê";
        string? parsedTargetCategory = null;
        string? parsedSpecificPreference = null;
        int partySize = 1;
        string parsedLocationType = "unspecified";
        string parsedSearchAnchor = "";
        bool needsClarification = false;
        string clarificationQuestion = "";
        string parsedDestination = "";
        var parsedOrigins = new List<(string Name, string PlaceName, string? Address, string Role)>();

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
            if (!match.Success)
            {
                logger.LogWarning("MiniMax response did not contain valid JSON: {Content}", content);
                return StatusCode(503, new { error = "Dịch vụ AI (MiniMax) không trả về định dạng phân tích hợp lệ. Vui lòng thử lại sau giây lát." });
            }

            using var doc = JsonDocument.Parse(match.Value);
            if (doc.RootElement.TryGetProperty("intent", out var iElem) && iElem.ValueKind == JsonValueKind.String)
            {
                parsedIntent = iElem.GetString() ?? "search";
            }

            if (doc.RootElement.TryGetProperty("party_size", out var psElem) && psElem.TryGetInt32(out var psVal))
            {
                partySize = Math.Max(1, psVal);
            }

            if (doc.RootElement.TryGetProperty("location_type", out var ltElem) && ltElem.ValueKind == JsonValueKind.String)
            {
                parsedLocationType = ltElem.GetString()?.Trim().ToLowerInvariant() ?? "unspecified";
            }

            if (doc.RootElement.TryGetProperty("query", out var qElem) && qElem.ValueKind == JsonValueKind.String)
            {
                parsedQuery = qElem.GetString() ?? "quán cà phê";
            }

            if (doc.RootElement.TryGetProperty("target_category", out var tcElem) && tcElem.ValueKind == JsonValueKind.String)
            {
                parsedTargetCategory = tcElem.GetString();
            }

            if (doc.RootElement.TryGetProperty("specific_preference", out var spElem) && spElem.ValueKind == JsonValueKind.String)
            {
                parsedSpecificPreference = spElem.GetString();
            }

            if (doc.RootElement.TryGetProperty("search_anchor", out var saElem) && saElem.ValueKind == JsonValueKind.String)
            {
                parsedSearchAnchor = saElem.GetString()?.Trim() ?? "";
            }

            if (doc.RootElement.TryGetProperty("needs_clarification", out var ncElem) &&
                (ncElem.ValueKind == JsonValueKind.True || ncElem.ValueKind == JsonValueKind.False))
            {
                needsClarification = ncElem.GetBoolean();
            }

            if (doc.RootElement.TryGetProperty("clarification_question", out var cqElem) && cqElem.ValueKind == JsonValueKind.String)
            {
                clarificationQuestion = cqElem.GetString()?.Trim() ?? "";
            }

            if (doc.RootElement.TryGetProperty("destination", out var dElem) && dElem.ValueKind == JsonValueKind.String)
            {
                parsedDestination = dElem.GetString() ?? "";
            }

            if (doc.RootElement.TryGetProperty("origins", out var originsElem) && originsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var o in originsElem.EnumerateArray())
                {
                    var name = o.TryGetProperty("name", out var nElem) && nElem.ValueKind == JsonValueKind.String
                        ? nElem.GetString() ?? "Người"
                        : "Người";

                    string placeName = "";
                    if (o.TryGetProperty("place_name", out var pnElem) && pnElem.ValueKind == JsonValueKind.String)
                    {
                        placeName = pnElem.GetString() ?? "";
                    }
                    else if (o.TryGetProperty("location", out var lElem) && lElem.ValueKind == JsonValueKind.String)
                    {
                        placeName = lElem.GetString() ?? "";
                    }

                    string? addr = null;
                    if (o.TryGetProperty("address", out var aElem) && aElem.ValueKind == JsonValueKind.String)
                    {
                        var aStr = aElem.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(aStr))
                        {
                            addr = aStr;
                        }
                    }

                    var role = o.TryGetProperty("role", out var rElem) && rElem.ValueKind == JsonValueKind.String
                        ? rElem.GetString() ?? "member"
                        : "member";
                    parsedOrigins.Add((name, placeName, addr, role));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to call MiniMax LLM to parse meeting prompt.");
            return StatusCode(503, new { error = "Dịch vụ AI (MiniMax) hiện không khả dụng hoặc gặp sự cố kết nối. Vui lòng thử lại sau giây lát." });
        }

        var myLat = request.UserLatitude ?? 10.7769;
        var myLng = request.UserLongitude ?? 106.7009;

        // Xử lý Intent CHỈ ĐƯỜNG (Directions)
        if (string.Equals(parsedIntent, "directions", StringComparison.OrdinalIgnoreCase))
        {
            if (needsClarification || string.Equals(parsedLocationType, "unspecified", StringComparison.OrdinalIgnoreCase))
            {
                var question = !string.IsNullOrWhiteSpace(clarificationQuestion)
                    ? clarificationQuestion
                    : "Bạn muốn tìm đường đi từ đâu đến địa điểm nào ạ?";

                return Ok(new MeetingSearchPromptResponse(
                    Query: parsedQuery,
                    Centroid: null,
                    Origins: [],
                    Results: [],
                    AiInsight: question,
                    SupportedAreas: activeAreaNames
                ));
            }

            var destTarget = !string.IsNullOrWhiteSpace(parsedDestination) ? parsedDestination : parsedQuery;
            var originTarget = parsedOrigins.Count > 0 && !string.IsNullOrWhiteSpace(parsedOrigins[0].PlaceName)
                ? parsedOrigins[0].PlaceName
                : "GPS_CURRENT";
            var originAddr = parsedOrigins.Count > 0 ? parsedOrigins[0].Address : null;
            var memberName = parsedOrigins.Count > 0 ? parsedOrigins[0].Name : "Bạn";

            double startLat = myLat;
            double startLng = myLng;
            string startName = "Vị trí của bạn";

            if (!string.Equals(originTarget, "GPS_CURRENT", StringComparison.OrdinalIgnoreCase))
            {
                var geocodedOrigin = await geocodingService.GeocodeAsync(originTarget, originAddr, cancellationToken);
                if (geocodedOrigin is not null)
                {
                    startLat = geocodedOrigin.Latitude;
                    startLng = geocodedOrigin.Longitude;
                    startName = geocodedOrigin.DisplayName ?? originTarget;
                }
                else
                {
                    return Ok(new MeetingSearchPromptResponse(
                        Query: parsedQuery,
                        Centroid: null,
                        Origins: [],
                        Results: [],
                        AiInsight: $"Hệ thống không tìm thấy toạ độ điểm xuất phát '{originTarget}' trong các khu vực hỗ trợ ({areasDescription}) để chỉ đường.",
                        SupportedAreas: activeAreaNames
                    ));
                }
            }

            var geocodedDest = await geocodingService.GeocodeAsync(destTarget, null, cancellationToken);
            if (geocodedDest is null)
            {
                return Ok(new MeetingSearchPromptResponse(
                    Query: parsedQuery,
                    Centroid: null,
                    Origins: [],
                    Results: [],
                    AiInsight: $"Hệ thống không tìm thấy toạ độ địa điểm đến '{destTarget}' trong các khu vực hỗ trợ ({areasDescription}) để tính đường đi.",
                    SupportedAreas: activeAreaNames
                ));
            }

            try
            {
                var routeResult = await routeService.GetRouteAsync(
                    new Search.Application.Abstractions.GeoCoordinate(startLat, startLng),
                    new Search.Application.Abstractions.GeoCoordinate(geocodedDest.Latitude, geocodedDest.Longitude),
                    cancellationToken
                );
                if (routeResult is not null && routeResult.Coordinates.Count > 0)
                {
                    var attachedRoute = new AttachedRoute(
                        OriginName: startName,
                        OriginLatitude: startLat,
                        OriginLongitude: startLng,
                        DestinationName: geocodedDest.DisplayName ?? destTarget,
                        DestinationLatitude: geocodedDest.Latitude,
                        DestinationLongitude: geocodedDest.Longitude,
                        MemberName: memberName,
                        DistanceKm: routeResult.DistanceKm,
                        DurationMinutes: routeResult.DurationMinutes,
                        Summary: $"Lộ trình từ {startName} đến {geocodedDest.DisplayName ?? destTarget}",
                        Coordinates: routeResult.Coordinates.Select(c => (IReadOnlyList<double>)new[] { c.Latitude, c.Longitude }).ToList()
                    );

                    return Ok(new MeetingSearchPromptResponse(
                        Query: destTarget,
                        Centroid: new MeetingSearchCentroid(geocodedDest.Latitude, geocodedDest.Longitude),
                        Origins: [
                            new MeetingSearchOriginItem(
                                Id: "route_origin",
                                Name: memberName,
                                Address: startName,
                                Lat: startLat,
                                Lng: startLng,
                                AvatarUrl: null,
                                IsMe: true
                            )
                        ],
                        Results: [],
                        AiInsight: $"Đã tìm thấy lộ trình cho {memberName} từ {startName} đến '{geocodedDest.DisplayName ?? destTarget}': khoảng cách {routeResult.DistanceKm:F1} km (~{Math.Round(routeResult.DurationMinutes)} phút lái xe). Lộ trình đã được vẽ trên bản đồ.",
                        SupportedAreas: activeAreaNames,
                        AttachedRoute: attachedRoute
                    ));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to calculate OSRM driving route for meeting search prompt.");
            }
        }

        // Xử lý Intent TÌM KIẾM / GỢI Ý ĐỊA ĐIỂM (Cá nhân hoặc Nhóm)
        var hasExplicitFriends = request.Friends is not null && request.Friends.Count > 0;

        // Trường hợp 1: MiniMax phân tích thấy yêu cầu chưa rõ địa điểm (unspecified) hoặc cần hỏi lại:
        // TUYỆT ĐỐI KHÔNG tự tiện lấy GPS cắm pin "Tôi", mà dừng lại hỏi người dùng làm rõ.
        if (!hasExplicitFriends &&
            (needsClarification || string.Equals(parsedLocationType, "unspecified", StringComparison.OrdinalIgnoreCase)) &&
            parsedOrigins.Count == 0 &&
            string.IsNullOrWhiteSpace(parsedSearchAnchor))
        {
            var question = !string.IsNullOrWhiteSpace(clarificationQuestion)
                ? clarificationQuestion
                : $"Bạn muốn tìm '{parsedQuery}' ở khu vực nào hoặc gần địa điểm cụ thể nào (ví dụ: {areasDescription})?";

            return Ok(new MeetingSearchPromptResponse(
                Query: parsedQuery,
                Centroid: null,
                Origins: [],
                Results: [],
                AiInsight: question,
                SupportedAreas: activeAreaNames
            ));
        }

        var resolvedOrigins = new List<MeetingSearchOriginItem>();
        var unresolvedOrigins = new List<(string Name, string Location)>();

        // Trường hợp 2: MiniMax xác định tìm kiếm quanh mốc địa danh (landmark)
        if (string.Equals(parsedLocationType, "landmark", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(parsedSearchAnchor) && parsedOrigins.Count == 0))
        {
            var anchorName = !string.IsNullOrWhiteSpace(parsedSearchAnchor) ? parsedSearchAnchor : parsedQuery;
            var geocodedAnchor = await geocodingService.GeocodeAsync(anchorName, null, cancellationToken);
            if (geocodedAnchor is not null)
            {
                resolvedOrigins.Add(new MeetingSearchOriginItem(
                    Id: "anchor",
                    Name: !string.IsNullOrWhiteSpace(geocodedAnchor.Name) ? geocodedAnchor.Name : anchorName,
                    Address: geocodedAnchor.DisplayName ?? geocodedAnchor.Name ?? anchorName,
                    Lat: geocodedAnchor.Latitude,
                    Lng: geocodedAnchor.Longitude,
                    AvatarUrl: null,
                    IsMe: false
                ));
            }
            else
            {
                return Ok(new MeetingSearchPromptResponse(
                    Query: parsedQuery,
                    Centroid: null,
                    Origins: [],
                    Results: [],
                    AiInsight: $"Hệ thống không tìm thấy toạ độ mốc địa danh '{anchorName}' trong các khu vực đang hỗ trợ ({areasDescription}). Bạn vui lòng chọn mốc địa danh khác hoặc kiểm tra lại nhé!",
                    SupportedAreas: activeAreaNames
                ));
            }
        }
        else
        {
            // Trường hợp 3: Vị trí hiện tại của người dùng (current_location) hoặc danh sách điểm xuất phát (custom_origins)
            if (string.Equals(parsedLocationType, "current_location", StringComparison.OrdinalIgnoreCase) && parsedOrigins.Count == 0)
            {
                parsedOrigins.Add(("Tôi", "GPS_CURRENT", null, "speaker"));
            }

            for (int i = 0; i < parsedOrigins.Count; i++)
            {
                var (oName, oPlace, oAddr, oRole) = parsedOrigins[i];
                var isGpsCurrent = string.Equals(oPlace, "GPS_CURRENT", StringComparison.OrdinalIgnoreCase);
                var isSpeaker = string.Equals(oRole, "speaker", StringComparison.OrdinalIgnoreCase) || oName.Equals("Tôi", StringComparison.OrdinalIgnoreCase);
                var isMe = isGpsCurrent || isSpeaker;
                var name = parsedOrigins.Count > 1
                    ? $"Người {i + 1}"
                    : (isMe ? "Tôi" : (!string.IsNullOrWhiteSpace(oName) && !oName.Equals("Người", StringComparison.OrdinalIgnoreCase) ? oName : "Vị trí của bạn"));

                if (isGpsCurrent || string.IsNullOrWhiteSpace(oPlace))
                {
                    resolvedOrigins.Add(new MeetingSearchOriginItem(
                        Id: isMe ? "me" : $"orig_{i}",
                        Name: name,
                        Address: "Vị trí GPS hiện tại",
                        Lat: myLat,
                        Lng: myLng,
                        AvatarUrl: null,
                        IsMe: isMe
                    ));
                }
                else
                {
                    var geocoded = await geocodingService.GeocodeAsync(oPlace, oAddr, cancellationToken);
                    if (geocoded is not null)
                    {
                        resolvedOrigins.Add(new MeetingSearchOriginItem(
                            Id: isMe ? "me" : $"orig_{i}",
                            Name: name,
                            Address: geocoded.DisplayName ?? (string.IsNullOrEmpty(oAddr) ? oPlace : $"{oPlace}, {oAddr}"),
                            Lat: geocoded.Latitude,
                            Lng: geocoded.Longitude,
                            AvatarUrl: null,
                            IsMe: isMe
                        ));
                    }
                    else
                    {
                        unresolvedOrigins.Add((name, string.IsNullOrEmpty(oAddr) ? oPlace : $"{oPlace}, {oAddr}"));
                    }
                }
            }
        }

        // Nếu có điểm xuất phát không tìm thấy trong dữ liệu hệ thống (nằm ở quận khác hoặc chưa có toạ độ),
        // TUYỆT ĐỐI KHÔNG tự bịa toạ độ, mà yêu cầu AI giải thích rõ ràng phạm vi dữ liệu hiện tại và xin xác nhận.
        if (unresolvedOrigins.Count > 0)
        {
            var unresolvablePrompt = $$"""
Người dùng muốn tìm điểm hẹn nhóm: "{{request.Prompt}}".
Cơ sở dữ liệu hệ thống hiện tại CHỈ MỚI CÓ DỮ LIỆU tại các khu vực được kích hoạt: {{areasDescription}}.
Các điểm xuất phát sau đây không tìm thấy trong cơ sở dữ liệu hệ thống: {{string.Join(", ", unresolvedOrigins.Select(u => $"{u.Name} ({u.Location})"))}}.

Nhiệm vụ của bạn:
- Giải thích rõ ràng, lịch sự cho người dùng biết rằng cơ sở dữ liệu hệ thống hiện tại chỉ mới có dữ liệu tại: {{areasDescription}}.
- Chỉ ra cụ thể những điểm xuất phát nào chưa có toạ độ trong dữ liệu hệ thống để người dùng nắm rõ.
- Do thiếu toạ độ chính xác từ dữ liệu, hệ thống không thể tự ý tính toán điểm hẹn hay lộ trình một cách tuỳ tiện.
- Đề nghị người dùng xác nhận hoặc chọn lại các điểm xuất phát nằm trong phạm vi khu vực hệ thống đang hỗ trợ ({{areasDescription}}).
- Trả lời bằng giọng điệu tự nhiên, thông minh, súc tích (1-2 đoạn ngắn), không dùng văn mẫu máy móc.
""";

            var insightMessages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, "Bạn là chuyên gia tư vấn điều phối điểm hẹn nhóm thông minh."),
                new(AgentModelRole.User, unresolvablePrompt)
            };

            string unresolvableInsight;
            try
            {
                var insightResp = await modelClient.SendAsync(insightMessages, [], cancellationToken);
                unresolvableInsight = insightResp.AssistantMessage?.Content?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to call MiniMax for unresolved origins insight.");
                unresolvableInsight = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(unresolvableInsight))
            {
                var missingList = string.Join(", ", unresolvedOrigins.Select(u => u.Location));
                unresolvableInsight = $"Hệ thống hiện tại chỉ mới có dữ liệu địa điểm tại các khu vực: {areasDescription}. Các điểm xuất phát ({missingList}) hiện chưa có trong cơ sở dữ liệu nên không thể tính toán toạ độ và lộ trình chính xác. Bạn vui lòng xác nhận hoặc chọn điểm xuất phát trong phạm vi các khu vực hệ thống hỗ trợ nhé!";
            }

            return Ok(new MeetingSearchPromptResponse(
                Query: parsedQuery,
                Centroid: null,
                Origins: resolvedOrigins,
                Results: [],
                AiInsight: unresolvableInsight,
                SupportedAreas: activeAreaNames
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
            TopK: 10,
            TargetCategory: parsedTargetCategory,
            SpecificPreference: parsedSpecificPreference
        );

        var searchResult = await meetingPlacesHandler.HandleAsync(queryObj, cancellationToken);

        var finalResults = searchResult.Results.ToList();
        string? insight = null;
        var isAnchorSearch = resolvedOrigins.Count == 1 && resolvedOrigins[0].Id == "anchor";

        if (finalResults.Count > 0)
        {
            try
            {
                var originsSummary = string.Join(", ", resolvedOrigins.Select(o => $"{o.Name} ({o.Address})"));
                var isSingleOrigin = resolvedOrigins.Count <= 1;

                // Chuẩn bị danh sách các địa điểm ứng viên để MiniMax rà soát tính toàn vẹn (Tier 2 Integrity Verification)
                var candidateListPrompt = string.Join("\n", finalResults.Select((r, idx) =>
                    $"- [{r.PlaceId}] {r.Name} | Thể loại: {r.Category ?? "Chưa rõ"} | Địa chỉ: {r.Address ?? "TP.HCM"} | Cách: {r.AverageDistanceKm:F1}km (~{(r.AverageDurationMinutes ?? (r.AverageDistanceKm / 25.0 * 60.0)):F0}p)"));

                var verifierSystemPrompt =
                    "Bạn là chuyên gia thẩm định và điều phối điểm hẹn thông minh của hệ thống LOSM.\n" +
                    "Nhiệm vụ của bạn là rà soát (verify/filter) danh sách các địa điểm ứng viên hệ thống vừa tìm được so với yêu cầu ban đầu của người dùng:\n" +
                    "1. Kiểm tra kỹ: người dùng đang muốn tìm thể loại/món ăn hay đặc điểm cụ thể nào (ví dụ: món Nhật, ramen, sushi, cafe, quán chay, quán nhậu, v.v.).\n" +
                    "2. Thẩm định từng địa điểm trong danh sách ứng viên:\n" +
                    "   - Giữ lại và ưu tiên các địa điểm THỰC SỰ ĐÚNG với yêu cầu của người dùng.\n" +
                    "   - Loại bỏ các địa điểm hoàn toàn lạc đề hoặc không đúng thể loại (ví dụ: người dùng tìm quán Nhật mà địa điểm là quán thuần món Việt, quán lẩu mắm, quán cafe hay quán bia hơi).\n" +
                    "3. Viết 1-2 câu nhận xét tư vấn (ai_insight) thật tự nhiên, súc tích và hữu ích:\n" +
                    "   - Nếu có địa điểm phù hợp: nêu lý do quán đứng đầu là lựa chọn lý tưởng nhất cho nhóm.\n" +
                    "   - Nếu KHÔNG CÓ địa điểm nào trong danh sách thực sự đúng yêu cầu cụ thể của người dùng: thông báo trung thực rằng cơ sở dữ liệu hiện tại chưa có quán phù hợp với yêu cầu này trong khu vực, và trả về danh sách rỗng.\n" +
                    "4. Tuyệt đối không dùng văn mẫu máy móc hay khen một quán lạc đề là đúng món người dùng yêu cầu.\n\n" +
                    "Chỉ xuất ra DUY NHẤT một chuỗi JSON hợp lệ theo định dạng:\n" +
                    "{\n" +
                    "  \"valid_place_ids\": [\"id_1\", \"id_2\", ...],\n" +
                    "  \"ai_insight\": \"Lời tư vấn nhận xét ngắn gọn, tự nhiên...\"\n" +
                    "}";

                var verifierUserPrompt =
                    $"Yêu cầu ban đầu của người dùng: \"{request.Prompt}\"\n" +
                    $"Truy vấn bóc tách: \"{parsedQuery}\" (Nhóm: \"{parsedTargetCategory}\", Sở thích: \"{parsedSpecificPreference}\")\n" +
                    $"Thành viên / Điểm xuất phát: {originsSummary}\n\n" +
                    $"Danh sách các địa điểm ứng viên hệ thống tìm được:\n{candidateListPrompt}\n\n" +
                    "Hãy thẩm định danh sách trên và trả về kết quả JSON.";

                var verifierMessages = new List<AgentModelMessage>
                {
                    new(AgentModelRole.System, verifierSystemPrompt),
                    new(AgentModelRole.User, verifierUserPrompt)
                };

                var verifierResp = await modelClient.SendAsync(verifierMessages, [], cancellationToken);
                var rawContent = verifierResp.AssistantMessage?.Content?.Trim() ?? string.Empty;

                var jsonMatch = Regex.Match(rawContent, @"\{[\s\S]*\}");
                if (jsonMatch.Success)
                {
                    using var verifyDoc = JsonDocument.Parse(jsonMatch.Value);
                    if (verifyDoc.RootElement.TryGetProperty("valid_place_ids", out var vElem) && vElem.ValueKind == JsonValueKind.Array)
                    {
                        var validIds = new List<string>();
                        foreach (var idElem in vElem.EnumerateArray())
                        {
                            var s = idElem.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                validIds.Add(s);
                            }
                        }

                        if (validIds.Count > 0)
                        {
                            var placeDict = finalResults.ToDictionary(r => r.PlaceId.ToString(), r => r, StringComparer.OrdinalIgnoreCase);
                            var reordered = new List<SearchMeetingPlaceResult>();
                            foreach (var vid in validIds)
                            {
                                if (placeDict.TryGetValue(vid, out var p))
                                {
                                    reordered.Add(p);
                                }
                            }

                            if (reordered.Count > 0)
                            {
                                finalResults = reordered;
                            }
                        }
                        else
                        {
                            finalResults = [];
                        }
                    }

                    if (verifyDoc.RootElement.TryGetProperty("ai_insight", out var insElem) && insElem.ValueKind == JsonValueKind.String)
                    {
                        var insStr = insElem.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(insStr))
                        {
                            insight = insStr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to verify meeting places with MiniMax, keeping original ranking.");
            }

            if (string.IsNullOrWhiteSpace(insight))
            {
                if (finalResults.Count > 0)
                {
                    var top = finalResults[0];
                    if (isAnchorSearch)
                    {
                        insight = $"Địa điểm phù hợp nhất gần {resolvedOrigins[0].Name} là \"{top.Name}\" ({top.Address ?? "TP.HCM"}), cách mốc khoảng {top.AverageDistanceKm:F1} km.";
                    }
                    else if (resolvedOrigins.Count <= 1)
                    {
                        insight = $"Địa điểm gợi ý phù hợp nhất cho bạn là \"{top.Name}\" ({top.Address ?? "TP.HCM"}), cách vị trí của bạn khoảng {top.AverageDistanceKm:F1} km.";
                    }
                    else
                    {
                        insight = $"Điểm hẹn tối ưu nhất cho nhóm là \"{top.Name}\" ({top.Address ?? "TP.HCM"}). Khoảng cách trung bình giữa các thành viên là {top.AverageDistanceKm:F1} km với độ công bằng lộ trình đạt {Math.Round(top.FairnessScore * 100)}%.";
                    }
                }
                else
                {
                    insight = $"Hệ thống chưa tìm thấy địa điểm nào đúng theo yêu cầu \"{parsedQuery}\" trong khu vực hỗ trợ ({areasDescription}). Bạn có thể thử thay đổi tiêu chí tìm kiếm nhé!";
                }
            }
        }
        else
        {
            var emptySearchPrompt = isAnchorSearch
                ? $$"""
Người dùng muốn tìm: "{{parsedQuery}}" ở gần mốc "{{resolvedOrigins[0].Name}}".
Hệ thống đang có dữ liệu tại: {{areasDescription}}.
Trong bán kính tìm kiếm quanh mốc này hiện chưa tìm thấy địa điểm nào phù hợp với '{{parsedQuery}}'.
Hãy giải thích ngắn gọn, tự nhiên và gợi ý người dùng đổi thể loại địa điểm hoặc mở rộng bán kính tìm kiếm.
"""
                : $$"""
Người dùng muốn tìm: "{{parsedQuery}}" cho nhóm {{resolvedOrigins.Count}} người.
Hệ thống đang có dữ liệu tại: {{areasDescription}}.
Tâm điểm nhóm đã được tính toán nhưng không tìm thấy địa điểm nào phù hợp với từ khoá '{{parsedQuery}}' trong cơ sở dữ liệu và bán kính tìm kiếm.
Hãy giải thích ngắn gọn, tự nhiên và gợi ý người dùng đổi thể loại địa điểm (ví dụ cà phê, quán ăn) hoặc nới rộng bán kính tìm kiếm.
""";

            var insightMessages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, "Bạn là chuyên gia tư vấn điều phối điểm hẹn nhóm thông minh."),
                new(AgentModelRole.User, emptySearchPrompt)
            };

            string emptyInsight;
            try
            {
                var insightResp = await modelClient.SendAsync(insightMessages, [], cancellationToken);
                emptyInsight = insightResp.AssistantMessage?.Content?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to call MiniMax for empty search insight.");
                emptyInsight = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(emptyInsight))
            {
                emptyInsight = isAnchorSearch
                    ? $"Trong phạm vi dữ liệu hệ thống ({areasDescription}), hiện chưa tìm thấy địa điểm '{parsedQuery}' nào gần {resolvedOrigins[0].Name}. Bạn thử đổi sang thể loại khác hoặc nới rộng bán kính xem sao nhé!"
                    : $"Trong phạm vi dữ liệu hệ thống ({areasDescription}), hiện không tìm thấy địa điểm '{parsedQuery}' nào phù hợp cho nhóm. Bạn thử đổi sang thể loại khác như quán cà phê hoặc quán ăn xem sao nhé!";
            }

            insight = emptyInsight;
        }

        var resultDtos = finalResults.Select(r => new MeetingPlaceResultDto(
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
            IsRoadRouting: r.IsRoadRouting,
            IsOnnxScored: r.IsOnnxScored
        )).ToList();

        var response = new MeetingSearchPromptResponse(
            Query: parsedQuery,
            Centroid: new MeetingSearchCentroid(searchResult.CentroidLatitude, searchResult.CentroidLongitude),
            Origins: resolvedOrigins,
            Results: resultDtos,
            AiInsight: insight,
            SupportedAreas: activeAreaNames,
            RankingMethod: searchResult.RankingMethod
        );

        return Ok(response);
    }
}
