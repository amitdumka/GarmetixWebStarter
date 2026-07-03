namespace Garmetix.Api.Auth;

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
