using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;

namespace Garmetix.Api.Production;

public static class Stage10LProductionSupportEndpoints
{
    public static RouteGroupBuilder MapStage10LProductionSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stage10l/production-support")
            .WithTags("Stage 10L Production Support")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", Summary);
        group.MapGet("/drills", Drills);

        return group;
    }

    private static IResult Summary(HttpContext httpContext, ILoggerFactory loggerFactory)
    {
        try
        {
            var request = httpContext.Request;
            var forwardedHost = request.Headers["X-Forwarded-Host"].ToString();
            var forwardedProto = request.Headers["X-Forwarded-Proto"].ToString();
            var localHost = request.Host.Value ?? string.Empty;
            var publicHost = string.IsNullOrWhiteSpace(forwardedHost) ? localHost : forwardedHost;
            var publicScheme = string.IsNullOrWhiteSpace(forwardedProto) ? request.Scheme : forwardedProto;
            var publicOrigin = string.IsNullOrWhiteSpace(publicHost) ? string.Empty : $"{publicScheme}://{publicHost}";
            var drills = BuildDrills();

            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                stage = AppInfoEndpoints.Stage,
                releaseName = AppInfoEndpoints.ReleaseName,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                overallStatus = "Support drills ready",
                publicOrigin,
                forwardedHost,
                forwardedProto,
                localHost,
                tunnelHint = publicHost.Contains("trycloudflare", StringComparison.OrdinalIgnoreCase)
                    || publicHost.Contains("cloudflare", StringComparison.OrdinalIgnoreCase),
                drillCount = drills.Length,
                drills,
                warning = (string?)null
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10LProductionSupport").LogError(ex, "Stage 10L production support summary failed; returning safe degraded payload.");
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                stage = AppInfoEndpoints.Stage,
                releaseName = AppInfoEndpoints.ReleaseName,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                overallStatus = "Degraded - review logs",
                publicOrigin = string.Empty,
                forwardedHost = string.Empty,
                forwardedProto = string.Empty,
                localHost = string.Empty,
                tunnelHint = false,
                drillCount = 0,
                drills = Array.Empty<Stage10LSupportDrill>(),
                warning = ex.Message
            });
        }
    }

    private static IResult Drills(ILoggerFactory loggerFactory)
    {
        try
        {
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                items = BuildDrills().SelectMany(drill => drill.Steps.Select(step => new
                {
                    drill = drill.Title,
                    route = drill.Route,
                    severity = drill.Severity,
                    step
                }))
            });
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger("Stage10LProductionSupport").LogError(ex, "Stage 10L production support drills failed; returning safe degraded payload.");
            return Results.Ok(new
            {
                version = AppInfoEndpoints.Version,
                buildCode = AppInfoEndpoints.BuildCode,
                generatedAtUtc = DateTimeOffset.UtcNow,
                error = ex.Message,
                items = Array.Empty<object>()
            });
        }
    }

    private static Stage10LSupportDrill[] BuildDrills() =>
    [
        new("Save failure drill", "High", "/message-logs", "Failed save, validation error, FK error, duplicate number or request body issue.",
        [
            "Copy the visible save message without server URL.",
            "Open Message Logs and filter by Error or Failed around the same time.",
            "Open Runtime Diagnostics if the error mentions table, migration, sequence or database.",
            "Retry the same save only after confirming whether the previous attempt created a record."
        ],
        [
            "Message Logs",
            "Runtime Diagnostics",
            "System Health"
        ]),
        new("Print or PDF failure drill", "High", "/print-final-acceptance", "Voucher, invoice, petty cash, purchase return, payslip or salary payment print issue.",
        [
            "Use Download PDF first to confirm whether the server PDF is generated.",
            "Scan the document QR or barcode if a print was created.",
            "Open Message Logs for print/download errors and keep the document number.",
            "Run Print Final Acceptance after fixing printer, browser popup or hosted URL settings."
        ],
        [
            "Print Final Acceptance",
            "Document Scanner",
            "Message Logs"
        ]),
        new("Backup warning drill", "Critical", "/backup-maintenance", "Latest backup missing, restore drill stale, Google Drive status warning or disk issue.",
        [
            "Open Backup Maintenance and confirm latest local backup status.",
            "Run or review the disposable restore drill before any update.",
            "Open Production Readiness to confirm backup and restore warning state.",
            "Do not upgrade production until one valid backup and restore drill marker are present."
        ],
        [
            "Backup Maintenance",
            "Production Readiness",
            "Google Drive Backup"
        ]),
        new("Email or share failure drill", "Medium", "/email-delivery", "Payslip, voucher, alert, SMTP or notification delivery issue.",
        [
            "Open Email Delivery and verify masked SMTP configuration status.",
            "Send a test email to confirm provider login, timeout and sender configuration.",
            "Check Message Logs for SMTP provider response without exposing password or token values.",
            "Use PDF download as fallback evidence when email or WhatsApp share is not available."
        ],
        [
            "Email Delivery",
            "Message Logs",
            "Salary Payment PDF"
        ]),
        new("Tunnel or API mismatch drill", "Critical", "/runtime-diagnostics", "Cloudflare, reverse proxy, outside access or frontend calling localhost instead of hosted API.",
        [
            "Open this support page from the public URL and compare public origin with browser URL.",
            "Open Runtime Diagnostics and API Health through the same public host.",
            "Confirm frontend API requests do not use localhost when accessed from another machine.",
            "Restart Docker only after confirming env URL, proxy headers and tunnel routing."
        ],
        [
            "Runtime Diagnostics",
            "API Health",
            "System Health"
        ])
    ];

    private sealed record Stage10LSupportDrill(
        string Title,
        string Severity,
        string Route,
        string Symptom,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> Evidence);
}
