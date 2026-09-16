using AgentCore.Application.Abstractions;
using AgentCore.Domain.Entities;
using GoogleAuthFlows = global::Google.Apis.Auth.OAuth2.Flows;
using GoogleAuth = global::Google.Apis.Auth.OAuth2;
using GoogleCalendar = global::Google.Apis.Calendar.v3;
using GoogleServices = global::Google.Apis.Services;
using GoogleTokenResponse = global::Google.Apis.Auth.OAuth2.Responses.TokenResponse;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentCore.Infrastructure.Google;

public sealed class GoogleCalendarClient : IGoogleCalendarClient
{
    private readonly GoogleOptions _options;
    private readonly IUserGoogleTokenRepository _tokenRepository;
    private readonly ILogger<GoogleCalendarClient> _logger;

    public GoogleCalendarClient(
        IOptions<GoogleOptions> options,
        IUserGoogleTokenRepository tokenRepository,
        ILogger<GoogleCalendarClient> logger)
    {
        _options = options.Value;
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public async Task<CalendarEventResult> CreateEventAsync(
        Guid userId,
        CalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            return new CalendarEventResult(
                false,
                null,
                null,
                "Google OAuth chưa được cấu hình (ClientId/ClientSecret trống).");
        }

        var token = await _tokenRepository.GetByUserIdAsync(
            userId, cancellationToken);

        if (token is null)
        {
            return new CalendarEventResult(
                false,
                null,
                null,
                "User chưa kết nối Google Calendar.");
        }

        try
        {
            var flow = new GoogleAuthFlows.GoogleAuthorizationCodeFlow(
                new GoogleAuthFlows.GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new GoogleAuth.ClientSecrets
                    {
                        ClientId = _options.ClientId,
                        ClientSecret = _options.ClientSecret
                    },
                    Scopes = _options.Scopes
                });

            var credential = new GoogleAuth.UserCredential(
                flow,
                token.GoogleSub,
                new GoogleTokenResponse
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresInSeconds = (long)Math.Max(1,
                        (token.AccessTokenExpiresAt - DateTime.UtcNow).TotalSeconds),
                    IssuedUtc = DateTime.UtcNow,
                    Scope = token.Scopes
                });

            var initializer = new GoogleServices.BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _options.ApplicationName
            };

            using var service = new GoogleCalendar.CalendarService(initializer);

            var start = request.StartAtUtc;
            var end = start.AddMinutes(
                Math.Max(1, request.DurationMinutes));

            var newEvent = new GoogleCalendar.Data.Event
            {
                Summary = request.Title,
                Description = request.Description,
                Location = request.LocationAddress,
                Start = new GoogleCalendar.Data.EventDateTime
                {
                    DateTimeRaw = start.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    TimeZone = request.TimeZone
                },
                End = new GoogleCalendar.Data.EventDateTime
                {
                    DateTimeRaw = end.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    TimeZone = request.TimeZone
                }
            };

            if (request.AttendeeEmails is { Count: > 0 })
            {
                newEvent.Attendees = request.AttendeeEmails
                    .Select(email => new GoogleCalendar.Data.EventAttendee
                    {
                        Email = email
                    })
                    .ToList();
            }

            var insert = service.Events.Insert(newEvent, "primary");
            insert.ConferenceDataVersion = 0;
            insert.SendUpdates =
                GoogleCalendar.EventsResource.InsertRequest.SendUpdatesEnum.All;

            var created = await insert.ExecuteAsync(cancellationToken);

            return new CalendarEventResult(
                true,
                created.Id,
                created.HtmlLink,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create Google Calendar event for user {UserId}",
                userId);
            return new CalendarEventResult(false, null, null, ex.Message);
        }
    }
}