using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Auth;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions settings = options.Value;

    public bool IsEnabled => settings.Enabled;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!settings.Enabled)
        {
            throw new InvalidOperationException("Email delivery is disabled. Set Email:Enabled=true and configure SMTP settings.");
        }

        ValidateSettings(message);

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };

        mail.To.Add(new MailAddress(message.ToEmail, string.IsNullOrWhiteSpace(message.ToName) ? message.ToEmail : message.ToName));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, Encoding.UTF8, "text/plain"));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, "text/html"));

        if (!string.IsNullOrWhiteSpace(settings.ReplyToEmail))
        {
            mail.ReplyToList.Add(new MailAddress(settings.ReplyToEmail));
        }

        if (message.Attachments is { Count: > 0 })
        {
            foreach (var attachment in message.Attachments)
            {
                var stream = new MemoryStream(attachment.Content);
                mail.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }
        }

        using var smtp = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            smtp.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await smtp.SendMailAsync(mail, cancellationToken);
        logger.LogInformation("Email sent to {Email} with subject {Subject}.", message.ToEmail, message.Subject);
    }

    private void ValidateSettings(EmailMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.ToEmail))
        {
            throw new InvalidOperationException("Cannot send email because recipient email address is empty.");
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            throw new InvalidOperationException("Email:Host is required when email delivery is enabled.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromEmail))
        {
            throw new InvalidOperationException("Email:FromEmail is required when email delivery is enabled.");
        }
    }
}
