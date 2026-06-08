namespace Garmetix.Api.Auth;

public sealed record EmailMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody);

public interface IEmailSender
{
    bool IsEnabled { get; }

    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
