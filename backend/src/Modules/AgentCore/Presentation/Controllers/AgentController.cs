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

        var systemInstruction = """
Bạn là trợ lý AI thông minh của nền tảng bản đồ LOSM.
Nhiệm vụ: Phân tích câu yêu cầu của người dùng để trích xuất thông tin dưới dạng JSON:
1. "intent": "directions" (nếu người dùng hỏi đường đi, chỉ đường, cách di chuyển) hoặc "search" (mặc định cho tất cả yêu cầu tìm kiếm, gợi ý địa điểm cá nhân hoặc điểm hẹn nhóm).
2. "query": Thể loại địa điểm hoặc tên địa điểm người dùng muốn tìm (ví dụ: 'quán cà phê', 'quán lẩu', 'công viên', 'quán cơm'... hoặc tên quán cụ thể). Mặc định 'quán cà phê' nếu không rõ.
3. "target_category": Nhóm thể loại mục tiêu. BẮT BUỘC chọn chính xác 1 trong 13 nhóm sau nếu phù hợp:
   - "cafe": quán cà phê, trà, trà sữa, đồ uống giải khát...
   - "viet": nhà hàng, quán ăn món Việt, bún, phở, cơm tấm...
   - "japanese": nhà hàng Nhật Bản, sushi, ramen, sashimi...
   - "korean": nhà hàng Hàn Quốc, đồ nướng/lẩu Hàn...
   - "thai": nhà hàng Thái Lan, món Thái, tomyum...
   - "seafood": hải sản, quán ốc, hải sản tươi sống...
   - "western": món Âu, món Ý, món Pháp, pizza, steak, pasta, burger...
   - "vegetarian": đồ chay, quán chay, thuần chay...
   - "chinese": nhà hàng Trung Hoa, dim sum, lẩu Trung Hoa...
   - "bbq_hotpot": quán nướng, quán lẩu, buffet nướng lẩu nói chung...
   - "bookstore": nhà sách, hiệu sách, tiệm sách truyện...
   - "library": thư viện đọc sách...
   - "park": công viên, vườn hoa, quảng trường công viên...
   * LƯU Ý ĐẶC BIỆT: Nếu nhu cầu của người dùng KHÔNG THUỘC bất kỳ nhóm nào trong 13 nhóm trên (ví dụ: cây xăng, tiệm sửa xe, bệnh viện, ngân hàng, rạp phim, tiệm giặt, trạm y tế, khách sạn...), BẮT BUỘC trả về "other".
4. "specific_preference": Nếu người dùng có sở thích cụ thể về món ăn hoặc thương hiệu/thể loại con (ví dụ: "ramen", "sushi", "dim sum", "pizza", "món ý", "món pháp", "bún bò"...), hãy điền vào đây. Nếu không có hoặc yêu cầu chung chung, trả về chuỗi rỗng "".
5. "origins": Danh sách tất cả thành viên và địa điểm xuất phát:
   - "name": Tên hoặc định danh người đó (ví dụ: 'Tôi', 'Người 1', 'Bạn Nam').
   - "location": Địa điểm/vị trí xuất phát. CHỈ dùng "GPS_CURRENT" khi người dùng nói rõ: "ở vị trí của tôi", "vị trí hiện tại", "chỗ tôi".
   - "role": Vai trò của chủ thể ("speaker" hoặc "member"):
     + "speaker": Khi người dùng tự chỉ định rõ ràng bản thân mình đang ở vị trí đó (chủ thể ngôi thứ nhất gắn liền với vị trí).
     + "member": Dành cho bạn bè, người khác, hoặc khi người dùng chỉ liệt kê danh sách các điểm trong nhóm một cách khách quan (ví dụ: '1 đứa ở A, 1 đứa ở B, đứa còn lại ở C') mà không nói rõ bản thân ở điểm nào.
   - Nếu là cá nhân hỏi tìm kiếm bình thường cho bản thân (ví dụ: 'tìm quán cafe yên tĩnh', 'quán phở ngon gần đây'): để "origins" rỗng [].
6. "destination": Tên địa điểm đến (chỉ dùng khi intent là "directions").

Chỉ xuất ra DUY NHẤT một chuỗi JSON hợp lệ theo cấu trúc:
{
  "intent": "search",
  "query": "thể loại hoặc tên địa điểm",
  "target_category": "cafe",
  "specific_preference": "",
  "origins": [
    { "name": "Người 1", "location": "Điểm xuất phát 1", "role": "member" }
  ],
  "destination": ""
}
""";

        string parsedIntent = "search";
        string parsedQuery = "quán cà phê";
        string? parsedTargetCategory = null;
        string? parsedSpecificPreference = null;
        string parsedDestination = "";
        var parsedOrigins = new List<(string Name, string Location, string Role)>();

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
                    var loc = o.TryGetProperty("location", out var lElem) && lElem.ValueKind == JsonValueKind.String
                        ? lElem.GetString() ?? ""
                        : "";
                    var role = o.TryGetProperty("role", out var rElem) && rElem.ValueKind == JsonValueKind.String
                        ? rElem.GetString() ?? "member"
                        : "member";
                    parsedOrigins.Add((name, loc, role));
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
        if (string.Equals(parsedIntent, "directions", StringComparison.OrdinalIgnoreCase) ||
            request.Prompt.Contains("chỉ đường", StringComparison.OrdinalIgnoreCase) ||
            request.Prompt.Contains("đường đi", StringComparison.OrdinalIgnoreCase))
        {
            var destTarget = !string.IsNullOrWhiteSpace(parsedDestination) ? parsedDestination : parsedQuery;
            var originTarget = parsedOrigins.Count > 0 && !string.IsNullOrWhiteSpace(parsedOrigins[0].Location)
                ? parsedOrigins[0].Location
                : "GPS_CURRENT";
            var memberName = parsedOrigins.Count > 0 ? parsedOrigins[0].Name : "Bạn";

            double startLat = myLat;
            double startLng = myLng;
            string startName = "Vị trí của bạn";

            if (!string.Equals(originTarget, "GPS_CURRENT", StringComparison.OrdinalIgnoreCase))
            {
                var geocodedOrigin = await geocodingService.GeocodeAsync(originTarget, cancellationToken);
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

            var geocodedDest = await geocodingService.GeocodeAsync(destTarget, cancellationToken);
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
        if (parsedOrigins.Count == 0)
        {
            parsedOrigins.Add(("Tôi", "GPS_CURRENT", "speaker"));
        }

        var resolvedOrigins = new List<MeetingSearchOriginItem>();
        var unresolvedOrigins = new List<(string Name, string Location)>();

        for (int i = 0; i < parsedOrigins.Count; i++)
        {
            var (oName, oLoc, oRole) = parsedOrigins[i];
            var isGpsCurrent = string.Equals(oLoc, "GPS_CURRENT", StringComparison.OrdinalIgnoreCase);
            var isSpeaker = string.Equals(oRole, "speaker", StringComparison.OrdinalIgnoreCase) || oName.Equals("Tôi", StringComparison.OrdinalIgnoreCase);
            var isMe = isGpsCurrent || isSpeaker;
            var name = isMe ? "Tôi" : (!string.IsNullOrWhiteSpace(oName) && !oName.Equals("Người", StringComparison.OrdinalIgnoreCase) ? oName : $"Người {i + 1}");

            if (isGpsCurrent || string.IsNullOrWhiteSpace(oLoc))
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
                var geocoded = await geocodingService.GeocodeAsync(oLoc, cancellationToken);
                if (geocoded is not null)
                {
                    resolvedOrigins.Add(new MeetingSearchOriginItem(
                        Id: isMe ? "me" : $"orig_{i}",
                        Name: name,
                        Address: geocoded.DisplayName ?? oLoc,
                        Lat: geocoded.Latitude,
                        Lng: geocoded.Longitude,
                        AvatarUrl: null,
                        IsMe: isMe
                    ));
                }
                else
                {
                    unresolvedOrigins.Add((name, oLoc));
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

        string? insight = null;
        if (searchResult.Results.Count > 0)
        {
            var top = searchResult.Results[0];
            try
            {
                var originsSummary = string.Join(", ", resolvedOrigins.Select(o => $"{o.Name} ({o.Address})"));
                var isSingleOrigin = resolvedOrigins.Count <= 1;

                var promptSystem = isSingleOrigin
                    ? "Bạn là chuyên gia tư vấn địa điểm thông minh. " +
                      "Dựa trên vị trí của người dùng, hãy đưa ra 1-2 câu nhận xét tư vấn thật tự nhiên, súc tích và hữu ích cho địa điểm phù hợp nhất. " +
                      "Tuyệt đối không dùng văn mẫu máy móc hay lặp lại prompt."
                    : "Bạn là chuyên gia tư vấn điều phối điểm hẹn nhóm thông minh. " +
                      "Dựa trên kết quả tính toán lộ trình thực tế, hãy đưa ra 1-2 câu nhận xét tư vấn thật tự nhiên, súc tích và hữu ích cho người dùng. " +
                      "Nêu điểm hẹn tối ưu nhất, thời gian di chuyển của các thành viên hoặc vì sao điểm này công bằng, thuận tiện. " +
                      "Tuyệt đối không dùng văn mẫu máy móc hay lặp lại prompt.";

                var promptUser = isSingleOrigin
                    ? $"Người dùng ở: {originsSummary}. " +
                      $"Địa điểm phù hợp nhất tìm được là: '{top.Name}' ({top.Address ?? "TP.HCM"}). " +
                      $"Khoảng cách: {top.AverageDistanceKm:F1} km (~{(top.AverageDurationMinutes ?? (top.AverageDistanceKm / 25.0 * 60.0)):F0} phút). " +
                      $"Hãy tư vấn ngắn gọn, gợi ý lý do người dùng nên đến đây."
                    : $"Nhóm gồm: {originsSummary}. " +
                      $"Hệ thống đang có dữ liệu tại: {areasDescription}. " +
                      $"Điểm hẹn tối ưu nhất tìm được là: '{top.Name}' ({top.Address ?? "TP.HCM"}). " +
                      $"Thời gian di chuyển ước tính: {top.AverageDurationMinutes:F0} phút. Độ công bằng: {Math.Round(top.FairnessScore * 100)}%. " +
                      $"Hãy tư vấn ngắn gọn và tự nhiên cho nhóm.";

                var insightMessages = new List<AgentModelMessage>
                {
                    new(AgentModelRole.System, promptSystem),
                    new(AgentModelRole.User, promptUser)
                };

                var insightResp = await modelClient.SendAsync(insightMessages, [], cancellationToken);
                var rawInsight = insightResp.AssistantMessage?.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(rawInsight))
                {
                    insight = rawInsight;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate AI insight with MiniMax, using calculated fallback.");
            }

            if (string.IsNullOrWhiteSpace(insight))
            {
                insight = resolvedOrigins.Count <= 1
                    ? $"Địa điểm gợi ý phù hợp nhất cho bạn là \"{top.Name}\" ({top.Address ?? "TP.HCM"}), cách vị trí của bạn khoảng {top.AverageDistanceKm:F1} km."
                    : $"Điểm hẹn tối ưu nhất cho nhóm là \"{top.Name}\" ({top.Address ?? "TP.HCM"}). Khoảng cách trung bình giữa các thành viên là {top.AverageDistanceKm:F1} km với độ công bằng lộ trình đạt {Math.Round(top.FairnessScore * 100)}%.";
            }
        }
        else
        {
            var emptySearchPrompt = $$"""
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
                emptyInsight = $"Trong phạm vi dữ liệu hệ thống ({areasDescription}), hiện không tìm thấy địa điểm '{parsedQuery}' nào phù hợp cho nhóm. Bạn thử đổi sang thể loại khác như quán cà phê hoặc quán ăn xem sao nhé!";
            }

            insight = emptyInsight;
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
