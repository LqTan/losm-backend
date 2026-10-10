using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Agent.Queries.GetMeetingsForUser;
using AgentCore.Infrastructure.Email;
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
    private readonly IEmailSender _emailSender;
    private readonly IPendingActionStore _pendingActionStore;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<MeetingsController> _logger;

    public MeetingsController(
        IEmailSender emailSender,
        IPendingActionStore pendingActionStore,
        ILogger<MeetingsController> logger,
        IConfiguration? configuration = null)
    {
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
            return BadRequest(new { message = "Tiêu đề cuộc hẹn không được để trống." });

        if (string.IsNullOrWhiteSpace(request.PlaceName))
            return BadRequest(new { message = "Tên địa điểm không được để trống." });

        var validEmails = (request.AttendeeEmails ?? new List<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e) && EmailRegex.IsMatch(e.Trim()))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (validEmails.Count == 0)
            return BadRequest(new { message = "Vui lòng cung cấp ít nhất 1 email người tham gia hợp lệ." });

        var duration = request.DurationMinutes > 0 ? request.DurationMinutes : 60;
        var startUtc = request.StartAt.Kind == DateTimeKind.Utc
            ? request.StartAt
            : request.StartAt.ToUniversalTime();

        // 1. Tạo file lịch iCalendar (.ics)
        var icsBytes = BuildIcsAttachmentSafe(request, startUtc, duration, validEmails);

        var frontendBaseUrl = _configuration?["Frontend:BaseUrl"] ?? "http://localhost:3000";

        var sentEmails = new List<string>();
        var failedEmails = new List<string>();

        foreach (var email in validEmails)
        {
            try
            {
                var viewUrl = $"{frontendBaseUrl}/meetings/view?email={Uri.EscapeDataString(email)}";
                var htmlBody = BuildHtmlBody(request, startUtc, viewUrl);
                var plainBody = BuildTextBody(request, startUtc, viewUrl);

                var attachments = new List<EmailAttachment>();
                if (icsBytes is { Length: > 0 })
                {
                    attachments.Add(new($"invite-{startUtc:yyyyMMddHHmm}.ics", "text/calendar", icsBytes));
                }

                var res = await _emailSender.SendAsync(new EmailRequest(
                    ToAddress: email,
                    ToName: email,
                    Subject: $"[LOSM] Thư mời: {request.Title} @ {request.PlaceName}",
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
                    _logger.LogWarning("Không thể gửi email đến {Email}: {Error}", email, res.Error);
                }
            }
            catch (Exception ex)
            {
                failedEmails.Add(email);
                _logger.LogError(ex, "Lỗi gửi email cuộc hẹn đến {Email}", email);
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

                var confirmationId = Guid.NewGuid();
                var action = new AgentCore.Domain.Entities.PendingAgentAction(
                    id: Guid.NewGuid(),
                    sessionId: Guid.NewGuid(),
                    userId: userId,
                    actionType: "create_meeting",
                    payloadJson: payloadJson,
                    description: $"Cuộc hẹn '{request.Title}' tại '{request.PlaceName}' lúc {startUtc.AddHours(7):HH:mm dd/MM}",
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
                _logger.LogWarning(ex, "Không thể lưu lịch hẹn vào database cho user {UserId}", userId);
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
                ? $"Đã gửi thư mời cuộc hẹn thành công tới {sentEmails.Count} người tham gia!"
                : "Đã tạo lịch hẹn nhưng gửi email thất bại (vui lòng kiểm tra cấu hình SMTP)."
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
        List<string> attendees)
    {
        try
        {
            var calendar = new Calendar();
            var evt = new CalendarEvent
            {
                Uid = Guid.NewGuid().ToString("D"),
                Summary = request.Title,
                Description = $"Địa điểm: {request.PlaceName}\nĐịa chỉ: {request.Address ?? "N/A"}\n{request.Note ?? ""}",
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
            _logger.LogWarning(ex, "Không thể tạo file .ics cho cuộc hẹn: {Title}", request.Title);
            return null;
        }
    }

    private static string BuildHtmlBody(ScheduleMeetingRequest req, DateTime startUtc, string viewUrl)
    {
        var localTime = startUtc.AddHours(7); // Giờ Việt Nam (UTC+7)
        var host = string.IsNullOrWhiteSpace(req.HostName) ? "Một người bạn" : req.HostName;
        var address = string.IsNullOrWhiteSpace(req.Address) ? "" : $"<p style='color: #4b5563; margin: 6px 0; font-size: 14px;'><strong>📍 Địa chỉ:</strong> {System.Net.WebUtility.HtmlEncode(req.Address)}</p>";
        var note = string.IsNullOrWhiteSpace(req.Note) ? "" : $"<div style='background: #f9fafb; border: 1px dashed #d1d5db; padding: 12px; border-radius: 8px; margin: 14px 0; color: #374151; font-size: 13px;'><strong>Ghi chú:</strong> {System.Net.WebUtility.HtmlEncode(req.Note)}</div>";

        return $"""
        <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 580px; margin: 0 auto; padding: 24px; border: 1px solid #e5e7eb; border-radius: 12px; background: #ffffff;">
            <div style="text-align: center; margin-bottom: 24px;">
                <h1 style="color: #2563eb; margin: 0; font-size: 24px; font-weight: 700;">LOSM Meeting Invite</h1>
                <p style="color: #6b7280; font-size: 14px; margin-top: 4px;">Lời mời gặp mặt từ <strong>{System.Net.WebUtility.HtmlEncode(host)}</strong></p>
            </div>

            <div style="background: #eff6ff; border-left: 4px solid #3b82f6; padding: 16px; border-radius: 0 8px 8px 0; margin-bottom: 20px;">
                <h2 style="margin: 0 0 8px 0; color: #1e3a8a; font-size: 18px;">{System.Net.WebUtility.HtmlEncode(req.Title)}</h2>
                <p style="color: #1e40af; margin: 4px 0; font-size: 14px;">
                    📅 <strong>Thời gian:</strong> Lúc {localTime:HH:mm}, ngày {localTime:dd/MM/yyyy} (GMT+7)
                </p>
                <p style="color: #1e40af; margin: 4px 0; font-size: 14px;">
                    📍 <strong>Địa điểm:</strong> {System.Net.WebUtility.HtmlEncode(req.PlaceName)}
                </p>
            </div>

            {address}
            {note}

            <div style="text-align: center; margin: 28px 0 24px 0;">
                <a href="{viewUrl}" style="background-color: #2563eb; color: #ffffff; padding: 12px 28px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 14px; display: inline-block; box-shadow: 0 2px 4px rgba(37,99,235,0.2);">Xem chi tiết</a>
            </div>

            <div style="margin-top: 24px; padding-top: 16px; border-top: 1px solid #e5e7eb; text-align: center; color: #6b7280; font-size: 12px; line-height: 1.5;">
                <p>📎 Tệp lịch <strong>.ics</strong> đã được đính kèm để bạn có thể thêm nhanh vào Google Calendar, Apple Calendar hoặc Outlook.</p>
                <p style="margin-top: 6px;">Được gửi từ nền tảng tìm kiếm địa điểm và lên lịch <strong>LOSM</strong>.</p>
            </div>
        </div>
        """;
    }

    private static string BuildTextBody(ScheduleMeetingRequest req, DateTime startUtc, string viewUrl)
    {
        var localTime = startUtc.AddHours(7);
        return $"""
        LOSM Meeting Invite: {req.Title}
        ------------------------------------------
        Thời gian: Lúc {localTime:HH:mm}, ngày {localTime:dd/MM/yyyy} (GMT+7)
        Địa điểm: {req.PlaceName}
        Địa chỉ: {req.Address ?? "N/A"}
        Ghi chú: {req.Note ?? ""}
        ------------------------------------------
        Xem chi tiết lịch hẹn tại: {viewUrl}

        Tệp lịch .ics đính kèm hỗ trợ thêm tự động vào Calendar của bạn.
        """;
    }
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
