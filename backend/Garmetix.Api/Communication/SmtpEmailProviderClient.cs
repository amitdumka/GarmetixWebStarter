using Garmetix.Core.Models.Communication;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Garmetix.Api.Communication;

/// <summary>
/// The single reusable SMTP implementation serving every SMTP preset (Brevo SMTP, GoDaddy,
/// Microsoft 365, Gmail, custom, local Postfix) - presets only supply default Host/Port/TLS,
/// this client's connect/authenticate/send logic is identical for all of them. Uses MailKit
/// (not System.Net.Mail.SmtpClient, which is obsolete/limited on STARTTLS and cancellation).
/// NOTE: not exercised against a live SMTP account in this sandbox - flagged per this
/// project's established "no live click-through possible" disclosure convention.
/// </summary>
public sealed class SmtpEmailProviderClient : ITransactionalEmailProviderClient
{
    public async Task<EmailSendResult> SendAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        EmailSendRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.Host) || provider.Port is null)
        {
            return EmailSendResult.PermanentFailure("MissingSmtpHost", "SMTP provider has no Host/Port configured.");
        }

        var message = BuildMimeMessage(request);

        using var client = new SmtpClient();
        try
        {
            await ConnectAndAuthenticateAsync(client, provider, credentials, cancellationToken);
            var providerResponse = await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return EmailSendResult.Success(null, providerResponse);
        }
        catch (AuthenticationException ex)
        {
            return EmailSendResult.PermanentFailure("Smtp_AuthenticationFailed", $"SMTP authentication failed: {ex.Message}");
        }
        catch (SmtpCommandException ex) when (IsPermanentSmtpFailure(ex))
        {
            return EmailSendResult.PermanentFailure($"Smtp_{(int)ex.StatusCode}", SanitizeSmtpError(ex.Message));
        }
        catch (SmtpCommandException ex)
        {
            return EmailSendResult.TransientFailure($"Smtp_{(int)ex.StatusCode}", SanitizeSmtpError(ex.Message));
        }
        catch (Exception ex) when (ex is SmtpProtocolException or System.Net.Sockets.SocketException or TaskCanceledException)
        {
            return EmailSendResult.TransientFailure("Smtp_ConnectionError", $"SMTP connection error: {ex.Message}");
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    public async Task<EmailConnectionTestResult> TestConnectionAsync(
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider.Host) || provider.Port is null)
        {
            return new EmailConnectionTestResult(false, "No Host/Port configured for this SMTP provider.");
        }

        using var client = new SmtpClient();
        try
        {
            await ConnectAndAuthenticateAsync(client, provider, credentials, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return new EmailConnectionTestResult(true, $"Connected and authenticated to {provider.Host}:{provider.Port} successfully.");
        }
        catch (Exception ex)
        {
            return new EmailConnectionTestResult(false, $"SMTP connection test failed: {ex.Message}");
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    private static async Task ConnectAndAuthenticateAsync(
        SmtpClient client,
        EmailProviderConfiguration provider,
        IReadOnlyDictionary<string, string> credentials,
        CancellationToken cancellationToken)
    {
        var socketOptions = provider.UseStartTls
            ? SecureSocketOptions.StartTls
            : provider.EnableSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.None;

        await client.ConnectAsync(provider.Host!, provider.Port!.Value, socketOptions, cancellationToken);

        var hasUsername = credentials.TryGetValue("SmtpUsername", out var username) && !string.IsNullOrWhiteSpace(username);
        var hasPassword = credentials.TryGetValue("SmtpPassword", out var password) && !string.IsNullOrWhiteSpace(password);
        if (hasUsername && hasPassword)
        {
            await client.AuthenticateAsync(username!, password!, cancellationToken);
        }
    }

    private static MimeMessage BuildMimeMessage(EmailSendRequest request)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(request.From.DisplayName ?? request.From.Email, request.From.Email));
        foreach (var to in request.To)
        {
            message.To.Add(new MailboxAddress(to.DisplayName ?? to.Email, to.Email));
        }
        foreach (var cc in request.Cc ?? [])
        {
            message.Cc.Add(new MailboxAddress(cc.DisplayName ?? cc.Email, cc.Email));
        }
        foreach (var bcc in request.Bcc ?? [])
        {
            message.Bcc.Add(new MailboxAddress(bcc.DisplayName ?? bcc.Email, bcc.Email));
        }
        if (!string.IsNullOrWhiteSpace(request.ReplyToEmail))
        {
            message.ReplyTo.Add(new MailboxAddress(request.ReplyToEmail, request.ReplyToEmail));
        }

        // Subject travels through MimeKit's own header encoding (RFC 2047), which strips
        // CR/LF - this is the header-injection protection the security checklist asks for.
        message.Subject = request.Subject.Replace("\r", string.Empty).Replace("\n", string.Empty);

        var builder = new BodyBuilder
        {
            HtmlBody = request.HtmlBody,
            TextBody = request.TextBody,
        };
        foreach (var attachment in request.Attachments ?? [])
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static bool IsPermanentSmtpFailure(SmtpCommandException ex) =>
        ex.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.SenderNotAccepted
        || (int)ex.StatusCode is >= 500 and < 600;

    /// <summary>SMTP server responses can occasionally echo back the submitted envelope - strip anything that looks like credential material defensively.</summary>
    private static string SanitizeSmtpError(string message) =>
        message.Length > 500 ? message[..500] : message;
}
