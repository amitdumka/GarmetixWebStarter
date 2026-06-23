using Garmetix.Api.Auth;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Production;

public static class EmailDeliveryDiagnosticsEndpoints
{
    private static readonly string[] RequiredEnvironmentKeys =
    [
        "EMAIL_ENABLED",
        "EMAIL_HOST",
        "EMAIL_PORT",
        "EMAIL_ENABLE_SSL",
        "EMAIL_USERNAME",
        "EMAIL_PASSWORD",
        "EMAIL_FROM_EMAIL",
        "EMAIL_FROM_NAME",
        "EMAIL_REPLY_TO_EMAIL",
        "EMAIL_TIMEOUT_SECONDS",
        "PASSWORD_RESET_FRONTEND_BASE_URL"
    ];

    public static RouteGroupBuilder MapEmailDeliveryDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/email-diagnostics")
            .WithTags("Email Diagnostics")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", Status);
        group.MapPost("/send-test", SendTestAsync);

        return group;
    }

    private static IResult Status(IOptions<EmailOptions> options)
    {
        var settings = options.Value;
        var issues = new List<string>();

        if (!settings.Enabled)
        {
            issues.Add("Email delivery is disabled.");
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            issues.Add("SMTP host is missing.");
        }

        if (settings.Host.Contains("example.com", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("SMTP host still uses a placeholder value.");
        }

        if (settings.Port <= 0 || settings.Port > 65535)
        {
            issues.Add("SMTP port is invalid.");
        }

        if (settings.TimeoutSeconds < 5)
        {
            issues.Add("SMTP timeout should be at least 5 seconds.");
        }

        if (string.IsNullOrWhiteSpace(settings.FromEmail) || !settings.FromEmail.Contains('@'))
        {
            issues.Add("From email is missing or invalid.");
        }

        if (settings.FromEmail.Contains("example.com", StringComparison.OrdinalIgnoreCase)
            || settings.FromEmail.Contains("garmetix.local", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("From email still uses a placeholder/local value.");
        }

        if (settings.Enabled && string.IsNullOrWhiteSpace(settings.UserName))
        {
            issues.Add("SMTP username is empty. This is valid only for trusted internal relays.");
        }

        if (settings.Enabled && !string.IsNullOrWhiteSpace(settings.UserName) && string.IsNullOrWhiteSpace(settings.Password))
        {
            issues.Add("SMTP password/app key is empty.");
        }

        return Results.Ok(new EmailDeliveryStatusDto(
            settings.Enabled,
            MaskHost(settings.Host),
            settings.Port,
            settings.EnableSsl,
            MaskAddress(settings.UserName),
            MaskAddress(settings.FromEmail),
            settings.FromName,
            MaskAddress(settings.ReplyToEmail),
            issues.Count == 0,
            issues,
            DetectProvider(settings.Host),
            !string.IsNullOrWhiteSpace(settings.UserName),
            Math.Max(settings.TimeoutSeconds, 0),
            RequiredEnvironmentKeys));
    }

    private static async Task<IResult> SendTestAsync(
        EmailTestRequest request,
        IEmailSender emailSender,
        IOptions<EmailOptions> options,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var toEmail = request.ToEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@'))
        {
            return Results.BadRequest(new { message = "Enter a valid recipient email address." });
        }

        if (!emailSender.IsEnabled)
        {
            return Results.BadRequest(new { message = "Email delivery is disabled. Set EMAIL_ENABLED=true and configure SMTP first." });
        }

        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? "Garmetix SMTP test email"
            : request.Subject.Trim();

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? "This is a Garmetix production readiness test email."
            : request.Message.Trim();

        var settings = options.Value;
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var traceId = context.TraceIdentifier;
        var html = $"""
            <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
            <hr>
            <p><strong>Garmetix SMTP test</strong></p>
            <p>Provider: {System.Net.WebUtility.HtmlEncode(DetectProvider(settings.Host))}</p>
            <p>Sent at UTC: {System.Net.WebUtility.HtmlEncode(timestamp)}</p>
            <p>Trace ID: {System.Net.WebUtility.HtmlEncode(traceId)}</p>
            <p>From: {System.Net.WebUtility.HtmlEncode(settings.FromEmail)}</p>
            """;
        var text = string.Join(Environment.NewLine,
            message,
            string.Empty,
            "Garmetix SMTP test",
            $"Provider: {DetectProvider(settings.Host)}",
            $"Sent at UTC: {timestamp}",
            $"Trace ID: {traceId}",
            $"From: {settings.FromEmail}");

        try
        {
            await emailSender.SendAsync(new EmailMessage(toEmail, request.ToName?.Trim() ?? string.Empty, subject, html, text), cancellationToken);
            return Results.Ok(new
            {
                success = true,
                message = "Test email sent successfully.",
                toEmail = MaskAddress(toEmail),
                providerName = DetectProvider(settings.Host),
                sentAtUtc = timestamp,
                traceId
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or System.Net.Mail.SmtpException
            or System.Net.Sockets.SocketException
            or IOException
            or UnauthorizedAccessException)
        {
            return Results.BadRequest(new
            {
                success = false,
                message = "SMTP test email failed.",
                error = ex.Message,
                traceId
            });
        }
    }

    private static string DetectProvider(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not configured";
        }

        var host = value.Trim().ToLowerInvariant();
        if (host.Contains("brevo") || host.Contains("sendinblue")) return "Brevo SMTP";
        if (host.Contains("gmail") || host.Contains("googlemail")) return "Google Gmail SMTP";
        if (host.Contains("office365") || host.Contains("outlook") || host.Contains("microsoft")) return "Microsoft 365 / Outlook SMTP";
        if (host.Contains("sendgrid")) return "SendGrid SMTP";
        if (host.Contains("mailgun")) return "Mailgun SMTP";
        if (host.Contains("amazonaws") || host.Contains("email-smtp")) return "Amazon SES SMTP";
        if (host.Contains("zoho")) return "Zoho SMTP";
        return "Custom SMTP";
    }

    private static string MaskHost(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return "***";
        }

        return $"{trimmed[..2]}***{trimmed[^2..]}";
    }

    private static string MaskAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        var name = trimmed[..at];
        var domain = trimmed[(at + 1)..];
        var visibleName = name.Length <= 2 ? name[..1] : name[..Math.Min(2, name.Length)];
        return $"{visibleName}***@{domain}";
    }
}

public sealed record EmailDeliveryStatusDto(
    bool Enabled,
    string Host,
    int Port,
    bool EnableSsl,
    string UserName,
    string FromEmail,
    string FromName,
    string ReplyToEmail,
    bool Ready,
    IReadOnlyList<string> Issues,
    string ProviderName,
    bool UsingAuthentication,
    int TimeoutSeconds,
    IReadOnlyList<string> RecommendedEnvironmentKeys);

public sealed record EmailTestRequest(
    string ToEmail,
    string? ToName,
    string? Subject,
    string? Message);
