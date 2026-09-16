using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.Email;

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content
);

public sealed record EmailRequest(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    IReadOnlyList<EmailAttachment>? Attachments = null
);

public sealed record EmailResult(
    bool Sent,
    string? Error
);

public interface IEmailSender
{
    Task<EmailResult> SendAsync(
        EmailRequest request,
        CancellationToken cancellationToken = default);
}