using System.Net;
using System.Text;
using Garmetix.Core.Models.Authentication;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Auth;

public sealed class PasswordResetEmailService(
    IEmailSender emailSender,
    IOptions<PasswordResetOptions> options,
    ILogger<PasswordResetEmailService> logger)
{
    private readonly PasswordResetOptions resetOptions = options.Value;

    public bool IsEnabled => emailSender.IsEnabled;

    public async Task SendAsync(AppUser user, string resetUrl, string token, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            logger.LogWarning("Password reset email skipped for user {UserId} because no email address is configured.", user.Id);
            throw new InvalidOperationException("This user does not have an email address configured.");
        }

        var userName = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name;
        var subject = string.IsNullOrWhiteSpace(resetOptions.Subject)
            ? "Reset your Garmetix password"
            : resetOptions.Subject;

        var html = BuildHtml(userName, resetUrl, token, expiresAtUtc);
        var text = BuildText(userName, resetUrl, token, expiresAtUtc);

        await emailSender.SendAsync(new EmailMessage(user.Email, userName, subject, html, text), cancellationToken);
    }

    private static string BuildHtml(string userName, string resetUrl, string token, DateTime expiresAtUtc)
    {
        var safeName = WebUtility.HtmlEncode(userName);
        var safeUrl = WebUtility.HtmlEncode(resetUrl);
        var safeToken = WebUtility.HtmlEncode(token);
        var expiresText = WebUtility.HtmlEncode($"{expiresAtUtc:yyyy-MM-dd HH:mm} UTC");

        return $$"""
            <!doctype html>
            <html>
            <body style="font-family:Arial,sans-serif;background:#f6f7fb;margin:0;padding:24px;color:#111827;">
              <div style="max-width:620px;margin:0 auto;background:#ffffff;border-radius:16px;padding:28px;border:1px solid #e5e7eb;">
                <h2 style="margin-top:0;color:#111827;">Reset your Garmetix password</h2>
                <p>Hello {{safeName}},</p>
                <p>We received a request to reset your Garmetix password.</p>
                <p style="margin:24px 0;">
                  <a href="{{safeUrl}}" style="background:#2563eb;color:#ffffff;text-decoration:none;padding:12px 18px;border-radius:10px;display:inline-block;font-weight:700;">Reset password</a>
                </p>
                <p>If the button does not work, open this link:</p>
                <p style="word-break:break-all;color:#2563eb;">{{safeUrl}}</p>
                <p>You can also copy this reset token into the Reset tab:</p>
                <pre style="white-space:pre-wrap;word-break:break-all;background:#f3f4f6;border-radius:10px;padding:12px;border:1px solid #e5e7eb;">{{safeToken}}</pre>
                <p>This reset link expires at <strong>{{expiresText}}</strong>.</p>
                <p>If you did not request this, you can ignore this email. Your password will not change until the token is used.</p>
              </div>
            </body>
            </html>
            """;
    }

    private static string BuildText(string userName, string resetUrl, string token, DateTime expiresAtUtc)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Hello {userName},");
        builder.AppendLine();
        builder.AppendLine("We received a request to reset your Garmetix password.");
        builder.AppendLine();
        builder.AppendLine("Reset link:");
        builder.AppendLine(resetUrl);
        builder.AppendLine();
        builder.AppendLine("Reset token:");
        builder.AppendLine(token);
        builder.AppendLine();
        builder.AppendLine($"This reset link expires at {expiresAtUtc:yyyy-MM-dd HH:mm} UTC.");
        builder.AppendLine("If you did not request this, you can ignore this email.");
        return builder.ToString();
    }
}
