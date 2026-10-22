using AgentCore.Infrastructure.PromptsManagement;
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
public sealed class AgentController(PromptProvider prompts) : ControllerBase
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
            return BadRequest("Request must not be empty.");
        }

        var allAreas = await areaRepo.GetAllAsync(cancellationToken);
        var activeAreaNames = allAreas.Where(a => a.IsActive).Select(a => a.Name).ToList();
        if (activeAreaNames.Count == 0)
        {
            activeAreaNames.Add("Quận 1");
        }
        var areasDescription = string.Join(", ", activeAreaNames);

        var systemInstruction = await ManagedTemplates.RenderAsync(prompts, "meeting.parse", cancellationToken, ("areasDescription", $"{areasDescription}"));

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
                return StatusCode(503, new { error = "The AI service returned an invalid analysis format. Please try again shortly." });
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
                        ? nElem.GetString() ?? "Person"
                        : "Person";

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
            return StatusCode(503, new { error = "The AI service is currently unavailable. Please try again shortly." });
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
                    : await ManagedTemplates.RenderAsync(prompts, "message.directions.clarify", cancellationToken);

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
            var memberName = parsedOrigins.Count > 0 ? parsedOrigins[0].Name : "You";

            double startLat = myLat;
            double startLng = myLng;
            string startName = "Your location";

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
                        AiInsight: await ManagedTemplates.RenderAsync(prompts, "message.directions.origin-missing", cancellationToken, ("originTarget", $"{originTarget}"), ("areasDescription", $"{areasDescription}")),
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
                    AiInsight: await ManagedTemplates.RenderAsync(prompts, "message.directions.destination-missing", cancellationToken, ("destTarget", $"{destTarget}"), ("areasDescription", $"{areasDescription}")),
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
                        Summary: $"Route from {startName} to {geocodedDest.DisplayName ?? destTarget}",
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
                        AiInsight: await ManagedTemplates.RenderAsync(prompts, "message.directions.result", cancellationToken, ("memberName", $"{memberName}"), ("startName", $"{startName}"), ("destinationName", $"{geocodedDest.DisplayName ?? destTarget}"), ("DistanceKm", $"{routeResult.DistanceKm:F1}"), ("durationMinutes", $"{Math.Round(routeResult.DurationMinutes)}")),
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
                : await ManagedTemplates.RenderAsync(prompts, "message.search.clarify", cancellationToken, ("parsedQuery", $"{parsedQuery}"), ("areasDescription", $"{areasDescription}"));

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
                var (isValidAnchor, anchorRejectionReason) = await ValidateAnchorWithLlmAsync(
                    modelClient,
                    request.Prompt,
                    anchorName,
                    geocodedAnchor,
                    areasDescription,
                    logger,
                    cancellationToken);

                if (!isValidAnchor)
                {
                    var rejectionMessage = !string.IsNullOrWhiteSpace(anchorRejectionReason)
                        ? anchorRejectionReason
                        : await ManagedTemplates.RenderAsync(prompts, "message.search.anchor-rejected", cancellationToken, ("anchorName", $"{anchorName}"), ("areasDescription", $"{areasDescription}"));

                    logger.LogInformation("LLM anchor validation rejected '{AnchorName}': matched '{MatchedName}' ({Address}) - Reason: {Reason}",
                        anchorName, geocodedAnchor.Name, geocodedAnchor.DisplayName, rejectionMessage);

                    return Ok(new MeetingSearchPromptResponse(
                        Query: parsedQuery,
                        Centroid: null,
                        Origins: [],
                        Results: [],
                        AiInsight: rejectionMessage,
                        SupportedAreas: activeAreaNames
                    ));
                }

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
                    AiInsight: await ManagedTemplates.RenderAsync(prompts, "message.search.anchor-missing", cancellationToken, ("anchorName", $"{anchorName}"), ("areasDescription", $"{areasDescription}")),
                    SupportedAreas: activeAreaNames
                ));
            }
        }
        else
        {
            // Trường hợp 3: Vị trí hiện tại của người dùng (current_location) hoặc danh sách điểm xuất phát (custom_origins)
            if (string.Equals(parsedLocationType, "current_location", StringComparison.OrdinalIgnoreCase) && parsedOrigins.Count == 0)
            {
                parsedOrigins.Add(("Me", "GPS_CURRENT", null, "speaker"));
            }

            for (int i = 0; i < parsedOrigins.Count; i++)
            {
                var (oName, oPlace, oAddr, oRole) = parsedOrigins[i];
                var isGpsCurrent = string.Equals(oPlace, "GPS_CURRENT", StringComparison.OrdinalIgnoreCase);
                var isSpeaker = string.Equals(oRole, "speaker", StringComparison.OrdinalIgnoreCase) || oName.Equals("Tôi", StringComparison.OrdinalIgnoreCase) || oName.Equals("Me", StringComparison.OrdinalIgnoreCase);
                var isMe = isGpsCurrent || isSpeaker;
                var name = parsedOrigins.Count > 1
                    ? $"Person {i + 1}"
                    : (isMe ? "Me" : (!string.IsNullOrWhiteSpace(oName) && !oName.Equals("Person", StringComparison.OrdinalIgnoreCase) ? oName : "Your location"));

                if (isGpsCurrent || string.IsNullOrWhiteSpace(oPlace))
                {
                    resolvedOrigins.Add(new MeetingSearchOriginItem(
                        Id: isMe ? "me" : $"orig_{i}",
                        Name: name,
                        Address: "Current GPS location",
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
                        var (isValidOrigin, _) = await ValidateAnchorWithLlmAsync(
                            modelClient,
                            request.Prompt,
                            oPlace,
                            geocoded,
                            areasDescription,
                            logger,
                            cancellationToken);

                        if (isValidOrigin)
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
            var unresolvablePrompt = await ManagedTemplates.RenderAsync(prompts, "meeting.unresolved", cancellationToken, ("userPrompt", $"{request.Prompt}"), ("areasDescription", $"{areasDescription}"), ("unresolvedOrigins", $"{string.Join(", ", unresolvedOrigins.Select(u => $"{u.Name} ({u.Location})"))}"));

            var insightMessages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, await ManagedTemplates.RenderAsync(prompts, "meeting.advisor", cancellationToken)),
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
                unresolvableInsight = await ManagedTemplates.RenderAsync(prompts, "message.search.origins-missing", cancellationToken, ("areasDescription", $"{areasDescription}"), ("missingList", $"{missingList}"));
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
                        Address: "Friend location",
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
                    $"- [{r.PlaceId}] {r.Name} | Category: {r.Category ?? "Unknown"} | Address: {r.Address ?? "TP.HCM"} | Distance: {r.AverageDistanceKm:F1}km (~{(r.AverageDurationMinutes ?? (r.AverageDistanceKm / 25.0 * 60.0)):F0}p)"));

                var verifierSystemPrompt =
                    await ManagedTemplates.RenderAsync(prompts, "meeting.verify.system", cancellationToken);

                var verifierUserPrompt =
                    await ManagedTemplates.RenderAsync(prompts, "meeting.verify.user", cancellationToken, ("Prompt", $"{request.Prompt}"), ("parsedQuery", $"{parsedQuery}"), ("parsedTargetCategory", $"{parsedTargetCategory}"), ("parsedSpecificPreference", $"{parsedSpecificPreference}"), ("originsSummary", $"{originsSummary}"), ("candidateListPrompt", $"{candidateListPrompt}"));

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
                        insight = await ManagedTemplates.RenderAsync(prompts, "message.search.anchor-result", cancellationToken, ("anchorName", $"{resolvedOrigins[0].Name}"), ("Name", $"{top.Name}"), ("Address", $"{top.Address ?? "TP.HCM"}"), ("AverageDistanceKm", $"{top.AverageDistanceKm:F1}"));
                    }
                    else if (resolvedOrigins.Count <= 1)
                    {
                        insight = await ManagedTemplates.RenderAsync(prompts, "message.search.single-result", cancellationToken, ("Name", $"{top.Name}"), ("Address", $"{top.Address ?? "TP.HCM"}"), ("AverageDistanceKm", $"{top.AverageDistanceKm:F1}"));
                    }
                    else
                    {
                        insight = await ManagedTemplates.RenderAsync(prompts, "message.search.group-result", cancellationToken, ("Name", $"{top.Name}"), ("Address", $"{top.Address ?? "TP.HCM"}"), ("AverageDistanceKm", $"{top.AverageDistanceKm:F1}"), ("fairnessPercent", $"{Math.Round(top.FairnessScore * 100)}"));
                    }
                }
                else
                {
                    insight = await ManagedTemplates.RenderAsync(prompts, "message.search.no-match", cancellationToken, ("parsedQuery", $"{parsedQuery}"), ("areasDescription", $"{areasDescription}"));
                }
            }
        }
        else
        {
            var emptySearchPrompt = isAnchorSearch
                ? await ManagedTemplates.RenderAsync(prompts, "meeting.empty.anchor", cancellationToken, ("parsedQuery", $"{parsedQuery}"), ("anchorName", $"{resolvedOrigins[0].Name}"), ("areasDescription", $"{areasDescription}"))
                : await ManagedTemplates.RenderAsync(prompts, "meeting.empty.group", cancellationToken, ("parsedQuery", $"{parsedQuery}"), ("memberCount", $"{resolvedOrigins.Count}"), ("areasDescription", $"{areasDescription}"));

            var insightMessages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, await ManagedTemplates.RenderAsync(prompts, "meeting.advisor", cancellationToken)),
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
                    ? await ManagedTemplates.RenderAsync(prompts, "message.search.empty-anchor", cancellationToken, ("areasDescription", $"{areasDescription}"), ("parsedQuery", $"{parsedQuery}"), ("anchorName", $"{resolvedOrigins[0].Name}"))
                    : await ManagedTemplates.RenderAsync(prompts, "message.search.empty-group", cancellationToken, ("areasDescription", $"{areasDescription}"), ("parsedQuery", $"{parsedQuery}"));
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

    private async Task<(bool IsValid, string? Reason)> ValidateAnchorWithLlmAsync(
        IAgentModelClient modelClient,
        string userPrompt,
        string requestedAnchor,
        GeocodeResult geocoded,
        string areasDescription,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var anchorValidationSystemPrompt =
                await ManagedTemplates.RenderAsync(prompts, "meeting.anchor.system", cancellationToken, ("areasDescription", $"{areasDescription}"));

            var anchorValidationUserPrompt =
                await ManagedTemplates.RenderAsync(prompts, "meeting.anchor.user", cancellationToken, ("userPrompt", $"{userPrompt}"), ("requestedAnchor", $"{requestedAnchor}"), ("Name", $"{geocoded.Name}"), ("DisplayName", $"{geocoded.DisplayName ?? geocoded.Name}"), ("Category", $"{geocoded.Category ?? "Unknown"}"), ("Latitude", $"{geocoded.Latitude}"), ("Longitude", $"{geocoded.Longitude}"), ("areasDescription", $"{areasDescription}"));

            var validationMessages = new List<AgentModelMessage>
            {
                new(AgentModelRole.System, anchorValidationSystemPrompt),
                new(AgentModelRole.User, anchorValidationUserPrompt)
            };

            var validationResp = await modelClient.SendAsync(validationMessages, [], cancellationToken);
            var valContent = validationResp.AssistantMessage?.Content?.Trim() ?? string.Empty;
            var match = Regex.Match(valContent, @"\{[\s\S]*\}");
            if (match.Success)
            {
                using var valDoc = JsonDocument.Parse(match.Value);
                if (valDoc.RootElement.TryGetProperty("is_valid", out var validElem) &&
                    validElem.ValueKind is JsonValueKind.False or JsonValueKind.True)
                {
                    var isValid = validElem.GetBoolean();
                    string? reason = null;
                    if (!isValid && valDoc.RootElement.TryGetProperty("reason", out var rElem) && rElem.ValueKind == JsonValueKind.String)
                    {
                        reason = rElem.GetString()?.Trim();
                    }

                    return (isValid, reason);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM anchor validation failed for '{RequestedAnchor}', proceeding with caution.", requestedAnchor);
        }

        return (true, null);
    }
}
