using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AgentCore.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailResult> SendAsync(
        EmailRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            return new EmailResult(false, "Smtp:Host chưa cấu hình.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            return new EmailResult(false, "Smtp:FromAddress chưa cấu hình.");
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _options.FromName,
                _options.FromAddress));
            message.To.Add(new MailboxAddress(request.ToName, request.ToAddress));
            message.Subject = request.Subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = request.HtmlBody
            };

            if (!string.IsNullOrWhiteSpace(request.PlainTextBody))
            {
                bodyBuilder.TextBody = request.PlainTextBody;
            }

            if (request.Attachments is { Count: > 0 })
            {
                foreach (var attachment in request.Attachments)
                {
                    bodyBuilder.Attachments.Add(
                        attachment.FileName,
                        attachment.Content,
                        ContentType.Parse(attachment.ContentType));
                }
            }

            message.Body = bodyBuilder.ToMessageBody();

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
                "Email sent to {To} subject '{Subject}'",
                request.ToAddress,
                request.Subject);

            return new EmailResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {To} subject '{Subject}'",
                request.ToAddress,
                request.Subject);
            return new EmailResult(false, ex.Message);
        }
    }
}