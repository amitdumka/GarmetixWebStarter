using Microsoft.Extensions.Options;

namespace Garmetix.Api.Licensing;

public sealed class LicenseEnforcementMiddleware(RequestDelegate next, IOptions<LicenseOptions> options)
{
    private static readonly string[] AllowedApiPrefixes =
    [
        "/api/health",
        "/api/auth/bootstrap-status",
        "/api/auth/bootstrap-admin",
        "/api/auth/login",
        "/api/auth/forgot-password",
        "/api/auth/reset-password",
        "/api/auth/me",
        "/api/license",
        "/api/app-info",
        "/api/test-automation",
        "/api/email-diagnostics/status"
    ];

    public async Task InvokeAsync(HttpContext context, LicenseActivationService licenseService)
    {
        var settings = options.Value;
        var path = context.Request.Path.Value ?? string.Empty;
        if (!settings.EnforcementEnabled
            || !settings.RequireLicenseForOperationalApis
            || !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || AllowedApiPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var status = licenseService.GetStatus();
        if (status.Valid)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Garmetix license activation is required before this operation can continue.",
            status.State,
            status.ProductCode,
            status.Issues,
            action = "Open /license-activation as Admin or Owner and activate a valid license key."
        });
    }
}
