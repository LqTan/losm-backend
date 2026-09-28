using System.Net;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Users.Application.Abstractions;

namespace Users.Infrastructure.Notifications;

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "LocationSearch";
    public string AdminPanelUrl { get; set; } = string.Empty;
}

public sealed class SmtpAccountNotifier : IAccountNotifier
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpAccountNotifier> _logger;

    public SmtpAccountNotifier(
        IOptions<SmtpOptions> options,
        ILogger<SmtpAccountNotifier> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task SendNewAccountAsync(
        NewAccountNotification notification,
        CancellationToken cancellationToken = default)
    {
        var loginUrl = string.IsNullOrWhiteSpace(_options.AdminPanelUrl)
            ? null
            : _options.AdminPanelUrl.TrimEnd('/') + "/login";

        var html = $"""
            <div style="font-family:system-ui,-apple-system,'Segoe UI',sans-serif;max-width:560px;margin:0 auto;padding:24px">
              <h2 style="margin:0 0 16px">Welcome to LocationSearch</h2>
              <p>Hi {WebUtility.HtmlEncode(notification.FullName)},</p>
              <p>An account has been created for you. Use the credentials below to sign in.</p>
              <table cellpadding="8" style="border-collapse:collapse;margin:16px 0">
                <tr><td style="font-weight:600">Username</td><td>{WebUtility.HtmlEncode(notification.Username)}</td></tr>
                <tr><td style="font-weight:600">Temporary password</td><td><code>{WebUtility.HtmlEncode(notification.TemporaryPassword)}</code></td></tr>
              </table>
              {(loginUrl is null ? string.Empty : $"<p><a href=\"{WebUtility.HtmlEncode(loginUrl)}\">Sign in to the admin panel</a></p>")}
              <p style="color:#64748b;font-size:13px">Please change your password after the first sign in.</p>
            </div>
            """;

        return SendAsync(
            notification.ToAddress,
            notification.FullName,
            "Your LocationSearch account is ready",
            html,
            cancellationToken);
    }

    public Task SendPasswordChangedAsync(
        PasswordChangedNotification notification,
        CancellationToken cancellationToken = default)
    {
        var html = $"""
            <div style="font-family:system-ui,-apple-system,'Segoe UI',sans-serif;max-width:560px;margin:0 auto;padding:24px">
              <h2 style="margin:0 0 16px">Password updated</h2>
              <p>Hi {WebUtility.HtmlEncode(notification.FullName)},</p>
              <p>The password for account <strong>{WebUtility.HtmlEncode(notification.Username)}</strong> was just reset by an administrator.</p>
              <p style="color:#64748b;font-size:13px">If you did not expect this, contact your administrator immediately.</p>
            </div>
            """;

        return SendAsync(
            notification.ToAddress,
            notification.FullName,
            "Your LocationSearch password was reset",
            html,
            cancellationToken);
    }

    private async Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogInformation(
                "SMTP chưa cấu hình, bỏ qua email '{Subject}' tới {To}",
                subject,
                toAddress);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _options.FromName,
                _options.FromAddress));
            message.To.Add(new MailboxAddress(toName, toAddress));
            message.Subject = subject;
            message.Body = new BodyBuilder
            {
                HtmlBody = htmlBody
            }.ToMessageBody();

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.UseStartTls
                    ? MailKit.Security.SecureSocketOptions.StartTls
                    : MailKit.Security.SecureSocketOptions.Auto,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.User))
            {
                await client.AuthenticateAsync(
                    _options.User,
                    _options.Password,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation(
                "Đã gửi email '{Subject}' tới {To}",
                subject,
                toAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Không gửi được email '{Subject}' tới {To}",
                subject,
                toAddress);
        }
    }
}
