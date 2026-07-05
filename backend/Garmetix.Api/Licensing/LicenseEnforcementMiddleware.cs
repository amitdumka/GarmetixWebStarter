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

    public async Task InvokeAsync(HttpContext context, Garmetix.Infrastructure.Data.GarmetixDbContext db, Garmetix.Infrastructure.Audit.AuditActorContext auditActor)
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

        if (auditActor.CompanyId == null)
        {
            // If they are not authenticated or don't have a CompanyId, we let the authorization middleware handle it
            await next(context);
            return;
        }

        var subscription = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.TenantSubscriptions, 
            s => s.CompanyId == auditActor.CompanyId.Value);

        if (subscription == null || (subscription.IsActive && subscription.ValidTo > System.DateTime.UtcNow))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Garmetix SaaS subscription is inactive or expired.",
            action = "Please renew your subscription to continue using Garmetix."
        });
    }
}
