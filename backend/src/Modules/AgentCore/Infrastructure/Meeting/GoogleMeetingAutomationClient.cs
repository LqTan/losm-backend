using AgentCore.Infrastructure.Email;
using AgentCore.Infrastructure.Google;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentCore.Infrastructure.Meeting;

public sealed class GoogleMeetingAutomationClient : AgentCore.Application.Abstractions.IMeetingAutomationClient
{
    private readonly IGoogleCalendarClient _calendarClient;
    private readonly IEmailSender _emailSender;
    private readonly AgentCore.Application.Abstractions.IUserGoogleTokenRepository _tokenRepository;
    private readonly MeetingOptions _meetingOptions;
    private readonly SmtpOptions _smtpOptions;
    private readonly ILogger<GoogleMeetingAutomationClient> _logger;

    public GoogleMeetingAutomationClient(
        IGoogleCalendarClient calendarClient,
        IEmailSender emailSender,
        AgentCore.Application.Abstractions.IUserGoogleTokenRepository tokenRepository,
        IOptions<MeetingOptions> meetingOptions,
        IOptions<SmtpOptions> smtpOptions,
        ILogger<GoogleMeetingAutomationClient> logger)
    {
        _calendarClient = calendarClient;
        _emailSender = emailSender;
        _tokenRepository = tokenRepository;
        _meetingOptions = meetingOptions.Value;
        _smtpOptions = smtpOptions.Value;
        _logger = logger;
    }

    public async Task<AgentCore.Application.Models.MeetingAutomationResult> TriggerMeetingAsync(
        System.Text.Json.JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var meeting = ParseMeetingPayload(payload);

        var userId = TryGetGuid(payload, "userId") ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return new AgentCore.Application.Models.MeetingAutomationResult(
                false, false, null, "userId missing in payload.");
        }

        var calendarResult = await _calendarClient.CreateEventAsync(
            userId,
            new CalendarEventRequest(
                Title: meeting.Title,
                Description: meeting.Purpose,
                StartAtUtc: meeting.StartAtUtc,
                DurationMinutes: meeting.DurationMinutes,
                TimeZone: _meetingOptions.CalendarTimeZone,
                LocationAddress: meeting.Address,
                Latitude: meeting.Latitude,
                Longitude: meeting.Longitude,
                AttendeeEmails: meeting.AttendeeEmails),
            cancellationToken);

        if (!calendarResult.Created)
        {
            return new AgentCore.Application.Models.MeetingAutomationResult(
                false,
                false,
                null,
                calendarResult.Error ?? "Google Calendar tạo event thất bại.");
        }

        var emailsSent = await SendInvitationsAsync(
            meeting,
            calendarResult,
            cancellationToken);

        if (!emailsSent)
        {
            return new AgentCore.Application.Models.MeetingAutomationResult(
                true,
                false,
                calendarResult.EventId,
                "Calendar đã tạo nhưng gửi email thất bại.");
        }

        return new AgentCore.Application.Models.MeetingAutomationResult(
            true,
            true,
            calendarResult.EventId,
            null);
    }

    public async Task<AgentCore.Application.Models.MeetingAutomationResult> ResendInvitationsAsync(
        System.Text.Json.JsonElement payload,
        CancellationToken cancellationToken = default)
    {
        var userId = TryGetGuid(payload, "userId") ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            return new AgentCore.Application.Models.MeetingAutomationResult(
                false, false, null, "userId missing in payload.");
        }

        var token = await _tokenRepository.GetByUserIdAsync(userId, cancellationToken);
        if (token is null)
        {
            return new AgentCore.Application.Models.MeetingAutomationResult(
                false, false, null, "User chưa kết nối Google Calendar.");
        }

        var sourceActionId = TryGetGuid(payload, "sourceActionId");
        if (sourceActionId is null)
        {
            return new AgentCore.Application.Models.MeetingAutomationResult(
                false, false, null, "sourceActionId missing.");
        }

        return new AgentCore.Application.Models.MeetingAutomationResult(
            false, false, null,
            "Retry không thể re-create calendar event từ trong GoogleMeetingAutomationClient; " +
            "hãy gọi lại create_meeting với cùng idempotencyKey.");
    }

    private async Task<bool> SendInvitationsAsync(
        MeetingPayloadData meeting,
        CalendarEventResult calendarResult,
        CancellationToken cancellationToken)
    {
        if (meeting.AttendeeEmails.Count == 0)
        {
            return true;
        }

        var icsBytes = _meetingOptions.AttachIcs
            ? BuildIcsAttachment(meeting, calendarResult)
            : null;

        var html = BuildHtmlBody(meeting, calendarResult);

        var firstError = string.Empty;
        foreach (var email in meeting.AttendeeEmails)
        {
            var attachments = icsBytes is null
                ? null
                : new List<EmailAttachment>
                {
                    new($"invite-{meeting.StartAtUtc:yyyyMMddHHmm}.ics",
                        "text/calendar",
                        icsBytes)
                };

            var result = await _emailSender.SendAsync(
                new EmailRequest(
                    ToAddress: email,
                    ToName: email,
                    Subject: $"[LocationSearch] {meeting.Title}",
                    HtmlBody: html,
                    PlainTextBody: BuildTextBody(meeting, calendarResult),
                    Attachments: attachments),
                cancellationToken);

            if (!result.Sent)
            {
                firstError = result.Error ?? firstError;
            }
        }

        return string.IsNullOrEmpty(firstError);
    }

    private byte[] BuildIcsAttachment(MeetingPayloadData meeting, CalendarEventResult result)
    {
        var calendar = new Calendar();
        var evt = new CalendarEvent
        {
            Uid = result.EventId ?? Guid.NewGuid().ToString(),
            Summary = meeting.Title,
            Description = meeting.Purpose ?? meeting.Note ?? string.Empty,
            Location = meeting.Address ?? string.Empty,
            Start = new CalDateTime(
                new DateTime(
                    meeting.StartAtUtc.Year,
                    meeting.StartAtUtc.Month,
                    meeting.StartAtUtc.Day,
                    meeting.StartAtUtc.Hour,
                    meeting.StartAtUtc.Minute,
                    0,
                    DateTimeKind.Utc)),
            End = new CalDateTime(
                new DateTime(
                    meeting.StartAtUtc.Year,
                    meeting.StartAtUtc.Month,
                    meeting.StartAtUtc.Day,
                    meeting.StartAtUtc.Hour,
                    meeting.StartAtUtc.Minute,
                    0,
                    DateTimeKind.Utc)
                .AddMinutes(meeting.DurationMinutes)),
            Organizer = new Organizer(_smtpOptions.FromAddress)
            {
                CommonName = _smtpOptions.FromName
            }
        };

        if (meeting.AttendeeEmails.Count > 0)
        {
            evt.Attendees = meeting.AttendeeEmails
                .Select(a => new Attendee(a)
                {
                    Rsvp = true,
                    Role = "REQ-PARTICIPANT",
                    ParticipationStatus = "NEEDS-ACTION"
                })
                .ToList();
        }

        calendar.Events.Add(evt);
        var serializer = new CalendarSerializer();
        var serialized = serializer.SerializeToString(calendar);
        return System.Text.Encoding.UTF8.GetBytes(serialized);
    }

    private static string BuildHtmlBody(MeetingPayloadData meeting, CalendarEventResult result)
    {
        var link = string.IsNullOrEmpty(result.HtmlLink)
            ? string.Empty
            : $"<p>Mở trong Google Calendar: <a href=\"{result.HtmlLink}\">{result.HtmlLink}</a></p>";
        var placeLine = string.IsNullOrEmpty(meeting.Address)
            ? string.Empty
            : $"<p>Địa điểm: <strong>{System.Net.WebUtility.HtmlEncode(meeting.Address)}</strong></p>";
        var purposeLine = string.IsNullOrEmpty(meeting.Purpose)
            ? string.Empty
            : $"<p>Mục đích: {System.Net.WebUtility.HtmlEncode(meeting.Purpose)}</p>";
        var noteLine = string.IsNullOrEmpty(meeting.Note)
            ? string.Empty
            : $"<p>Ghi chú: {System.Net.WebUtility.HtmlEncode(meeting.Note)}</p>";

        return $"""
            <p>Bạn có cuộc hẹn mới:</p>
            <h2>{System.Net.WebUtility.HtmlEncode(meeting.Title)}</h2>
            <p>Thời gian: <strong>{meeting.StartAtUtc:HH:mm dd/MM/yyyy} UTC</strong> ({meeting.DurationMinutes} phút)</p>
            {placeLine}
            {purposeLine}
            {noteLine}
            {link}
            <p>File lịch (.ics) đính kèm để Add to Calendar.</p>
            """;
    }

    private static string BuildTextBody(MeetingPayloadData meeting, CalendarEventResult result)
    {
        var lines = new List<string>
        {
            $"Cuộc hẹn: {meeting.Title}",
            $"Thời gian: {meeting.StartAtUtc:HH:mm dd/MM/yyyy} UTC ({meeting.DurationMinutes} phút)"
        };
        if (!string.IsNullOrEmpty(meeting.Address))
        {
            lines.Add($"Địa điểm: {meeting.Address}");
        }
        if (!string.IsNullOrEmpty(meeting.Purpose))
        {
            lines.Add($"Mục đích: {meeting.Purpose}");
        }
        if (!string.IsNullOrEmpty(result.HtmlLink))
        {
            lines.Add($"Mở trong Google Calendar: {result.HtmlLink}");
        }
        return string.Join("\n", lines);
    }

    private static Guid? TryGetGuid(System.Text.Json.JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var prop) &&
            prop.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var raw = prop.GetString();
            if (Guid.TryParse(raw, out var g)) return g;
        }
        return null;
    }

    private static MeetingPayloadData ParseMeetingPayload(System.Text.Json.JsonElement payload)
    {
        var startAt = DateTime.UtcNow.AddHours(1);
        if (payload.TryGetProperty("startAt", out var start) &&
            start.ValueKind == System.Text.Json.JsonValueKind.String &&
            DateTime.TryParse(start.GetString(), out var parsedStart))
        {
            startAt = parsedStart.ToUniversalTime();
        }

        var duration = 60;
        if (payload.TryGetProperty("durationMinutes", out var d) &&
            d.ValueKind == System.Text.Json.JsonValueKind.Number)
        {
            duration = d.GetInt32();
            if (duration <= 0) duration = 60;
        }

        var attendees = new List<string>();
        if (payload.TryGetProperty("attendees", out var att) &&
            att.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var a in att.EnumerateArray())
            {
                if (a.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var email = a.GetString();
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        attendees.Add(email);
                    }
                }
            }
        }

        var address = payload.TryGetProperty("address", out var addr) &&
            addr.ValueKind == System.Text.Json.JsonValueKind.String
            ? addr.GetString()
            : null;

        var latitude = payload.TryGetProperty("latitude", out var lat) &&
            lat.ValueKind == System.Text.Json.JsonValueKind.Number
            ? lat.GetDouble()
            : (double?)null;

        var longitude = payload.TryGetProperty("longitude", out var lon) &&
            lon.ValueKind == System.Text.Json.JsonValueKind.Number
            ? lon.GetDouble()
            : (double?)null;

        return new MeetingPayloadData(
            Title: payload.TryGetProperty("title", out var t) &&
                t.ValueKind == System.Text.Json.JsonValueKind.String
                ? t.GetString() ?? "Cuộc hẹn"
                : "Cuộc hẹn",
            Purpose: payload.TryGetProperty("purpose", out var p) &&
                p.ValueKind == System.Text.Json.JsonValueKind.String
                ? p.GetString()
                : null,
            Note: payload.TryGetProperty("note", out var n) &&
                n.ValueKind == System.Text.Json.JsonValueKind.String
                ? n.GetString()
                : null,
            StartAtUtc: startAt,
            DurationMinutes: duration,
            Address: address,
            Latitude: latitude,
            Longitude: longitude,
            AttendeeEmails: attendees);
    }

    private sealed record MeetingPayloadData(
        string Title,
        string? Purpose,
        string? Note,
        DateTime StartAtUtc,
        int DurationMinutes,
        string? Address,
        double? Latitude,
        double? Longitude,
        IReadOnlyList<string> AttendeeEmails);
}