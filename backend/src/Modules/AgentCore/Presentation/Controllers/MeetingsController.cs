using AgentCore.Infrastructure.PromptsManagement;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Agent.Queries.GetMeetingsForUser;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentCore.Presentation.Controllers;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController : ControllerBase
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private readonly PromptProvider? _prompts;
    private readonly IEmailSender _emailSender;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<MeetingsController> _logger;

    public MeetingsController(
        IEmailSender emailSender,
        IPendingActionStore pendingActionStore,
        ILogger<MeetingsController> logger,
        IConfiguration? configuration = null,
        PromptProvider? prompts = null)
    {
        _prompts = prompts;
        _emailSender = emailSender;
        _pendingActionStore = pendingActionStore;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("schedule")]
    [AllowAnonymous]
    public async Task<IActionResult> Schedule(
        [FromBody] ScheduleMeetingRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Meeting title is required." });

        if (string.IsNullOrWhiteSpace(request.PlaceName))
            return BadRequest(new { message = "Place name is required." });

        var validEmails = (request.AttendeeEmails ?? new List<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e) && EmailRegex.IsMatch(e.Trim()))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (validEmails.Count == 0)
            return BadRequest(new { message = "At least one valid attendee email address is required." });

        var duration = request.DurationMinutes > 0 ? request.DurationMinutes : 60;
        var startUtc = request.StartAt.Kind == DateTimeKind.Utc
            ? request.StartAt
            : request.StartAt.ToUniversalTime();

        // 1. Tạo file lịch iCalendar (.ics)
        var calendarDescription = await ManagedTemplates.RenderAsync(_prompts, "email.schedule.calendar", cancellationToken,
            ("place", request.PlaceName), ("address", request.Address ?? "N/A"), ("note", request.Note));
        var icsBytes = BuildIcsAttachmentSafe(request, startUtc, duration, validEmails, calendarDescription);
        var attachmentNotice = icsBytes is { Length: > 0 }
            ? await ManagedTemplates.RenderAsync(_prompts, "email.attachment-notice", cancellationToken) : string.Empty;
        var subject = await ManagedTemplates.RenderAsync(_prompts, "email.schedule.subject", cancellationToken,
            ("title", request.Title), ("place", request.PlaceName));

        var frontendBaseUrl = _configuration?["Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(frontendBaseUrl))
        {
            var origin = Request?.Headers?.Origin.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(origin))
            {
                frontendBaseUrl = origin;
            }
            else
            {
                var referer = Request?.Headers?.Referer.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refUri))
                {
                    frontendBaseUrl = $"{refUri.Scheme}://{refUri.Authority}";
                }
                else
                {
                    frontendBaseUrl = "http://localhost:3000";
                }
            }
        }
        frontendBaseUrl = frontendBaseUrl.TrimEnd('/');

        var actionId = Guid.NewGuid();
        var confirmationId = Guid.NewGuid();
        var sentEmails = new List<string>();
        var failedEmails = new List<string>();

        foreach (var email in validEmails)
        {
            try
            {
                var viewUrl = $"{frontendBaseUrl}/meetings/view?email={Uri.EscapeDataString(email)}&id={actionId}";
                var directionsUrl = (request.Latitude.HasValue && request.Longitude.HasValue)
                    ? $"{frontendBaseUrl}/?lat={request.Latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={request.Longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&name={Uri.EscapeDataString(request.PlaceName)}&address={Uri.EscapeDataString(request.Address ?? "")}&action=directions&origin=gps"
                    : $"{frontendBaseUrl}/?query={Uri.EscapeDataString(request.PlaceName)}&action=directions&origin=gps";

                var htmlBody = await BuildBodyAsync("email.schedule.html", request, startUtc, viewUrl, directionsUrl, attachmentNotice, cancellationToken);
                var plainBody = await BuildBodyAsync("email.schedule.text", request, startUtc, viewUrl, directionsUrl, attachmentNotice, cancellationToken);

                var attachments = new List<EmailAttachment>();
                if (icsBytes is { Length: > 0 })
                {
                    attachments.Add(new($"invite-{startUtc:yyyyMMddHHmm}.ics", "text/calendar; method=REQUEST; charset=UTF-8", icsBytes));
                }

                var res = await _emailSender.SendAsync(new EmailRequest(
                    ToAddress: email,
                    ToName: email,
                    Subject: subject,
                    HtmlBody: htmlBody,
                    PlainTextBody: plainBody,
                    Attachments: attachments
                ), cancellationToken);

                if (res.Sent)
                {
                    sentEmails.Add(email);
                }
                else
                {
                    failedEmails.Add(email);
                    _logger.LogWarning("Failed to send email to {Email}: {Error}", email, res.Error);
                }
            }
            catch (Exception ex)
            {
                failedEmails.Add(email);
                _logger.LogError(ex, "Failed to send meeting invitation to {Email}", email);
            }
        }

        // Lưu lịch hẹn vào database nếu user đã đăng nhập
        var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            try
            {
                var payloadJson = JsonSerializer.Serialize(new
                {
                    title = request.Title,
                    placeName = request.PlaceName,
                    address = request.Address,
                    latitude = request.Latitude,
                    longitude = request.Longitude,
                    startAt = startUtc,
                    durationMinutes = duration,
                    attendees = validEmails,
                    note = request.Note
                });
                var resultJson = JsonSerializer.Serialize(new
                {
                    calendarCreated = true,
                    emailsSent = sentEmails.Count > 0,
                    sentEmails,
                    failedEmails
                });

                var action = new AgentCore.Domain.Entities.PendingAgentAction(
                    id: actionId,
                    sessionId: Guid.NewGuid(),
                    userId: userId,
                    actionType: "create_meeting",
                    payloadJson: payloadJson,
                    description: $"Meeting '{request.Title}' at '{request.PlaceName}' on {startUtc.AddHours(7):HH:mm dd/MM}",
                    confirmationId: confirmationId,
                    idempotencyKey: Guid.NewGuid().ToString("N")
                );
                action.Confirm(confirmationId);
                action.StartExecution();
                action.Complete(resultJson);
                await _pendingActionStore.AddAsync(action, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save meeting for user {UserId}", userId);
            }
        }

        return Ok(new
        {
            success = true,
            totalAttendees = validEmails.Count,
            sentCount = sentEmails.Count,
            sentEmails,
            failedEmails,
            message = sentEmails.Count > 0
                ? $"Meeting invitations sent to {sentEmails.Count} attendees."
                : "Meeting created, but invitation delivery failed. Please check the SMTP configuration."
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetMyMeetings(
        [FromServices] GetMeetingsForUserHandler handler,
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await handler.HandleAsync(
            new GetMeetingsForUserQuery(userId, scope ?? MeetingScope.All, 100),
            cancellationToken);

        return Ok(result);
    }

    private byte[]? BuildIcsAttachmentSafe(
        ScheduleMeetingRequest request,
        DateTime startUtc,
        int durationMinutes,
        List<string> attendees, string description)
    {
        try
        {
            var calendar = new Calendar
            {
                Method = "REQUEST",
                ProductId = "-//LOSM//Meeting Platform//VI"
            };
            var evt = new CalendarEvent
            {
                Uid = Guid.NewGuid().ToString("D"),
                Summary = request.Title,
                Description = description,
                Location = string.IsNullOrWhiteSpace(request.Address) ? request.PlaceName : $"{request.PlaceName}, {request.Address}",
                Start = new CalDateTime(startUtc),
                End = new CalDateTime(startUtc.AddMinutes(durationMinutes))
            };

            evt.Attendees = attendees.Select(a =>
            {
                var uri = a.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ? a : $"mailto:{a}";
                return new Attendee(uri)
                {
                    CommonName = a,
                    Rsvp = true,
                    Role = "REQ-PARTICIPANT",
                    ParticipationStatus = "NEEDS-ACTION"
                };
            }).ToList();

            calendar.Events.Add(evt);
            var serializer = new CalendarSerializer();
            var serialized = serializer.SerializeToString(calendar);
            return System.Text.Encoding.UTF8.GetBytes(serialized);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create ICS attachment for meeting: {Title}", request.Title);
            return null;
        }
    }

    private Task<string> BuildBodyAsync(string key, ScheduleMeetingRequest req, DateTime startUtc,
        string viewUrl, string directionsUrl, string attachmentNotice, CancellationToken cancellationToken) =>
        ManagedTemplates.RenderAsync(_prompts, key, cancellationToken,
            ("host", string.IsNullOrWhiteSpace(req.HostName) ? "LOSM" : req.HostName),
            ("title", req.Title), ("time", startUtc.AddHours(7).ToString("HH:mm dd/MM/yyyy")),
            ("place", req.PlaceName), ("address", req.Address ?? "N/A"), ("note", req.Note),
            ("viewUrl", viewUrl), ("directionsUrl", directionsUrl), ("attachmentNotice", attachmentNotice));

}

public sealed record ScheduleMeetingRequest(
    string Title,
    string PlaceName,
    string? Address,
    double? Latitude,
    double? Longitude,
    DateTime StartAt,
    int DurationMinutes,
    List<string>? AttendeeEmails,
    string? Note,
    string? HostName
);
