using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Licensing;

/// <summary>
/// Blocks API access for a company once its SaaS subscription has actually expired - a real
/// consequence for the SaaS Manager module's token/activate flow, which previously only showed a
/// badge on the /subscription page with no enforcement teeth. Deliberately double-gated for safety:
/// (1) the master SaaSOptions.SubscriptionEnforcementEnabled switch, off by default like
/// LicenseEnforcementMiddleware's own switch, and (2) Company.SaaSClientId must be set - a company
/// nobody has ever linked to a SaaS client (which is every company that existed before this module)
/// is never touched by this middleware no matter what the switch says. A linked company that has
/// simply never had a subscription activated is trial-mode, not "expired" - it is only blocked once a
/// subscription row exists for it and that row's ValidTo has actually passed.
/// </summary>
public sealed class SaaSSubscriptionEnforcementMiddleware(RequestDelegate next, IOptions<SaaSOptions> options)
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
        "/api/saas",
        "/api/companies",
        "/api/app-info",
        "/api/test-automation",
        "/api/email-diagnostics/status"
    ];

    public async Task InvokeAsync(HttpContext context, GarmetixDbContext db)
    {
        var settings = options.Value;
        var path = context.Request.Path.Value ?? string.Empty;
        if (!settings.SubscriptionEnforcementEnabled
            || !path.StartsWith("/api", StringComparison.OrdinalIgnoreCase)
            || AllowedApiPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        var companyIdClaim = context.User.FindFirst("companyId")?.Value;
        if (!Guid.TryParse(companyIdClaim, out var companyId))
        {
            // No company scope on this token (SuperAdmin/Owner with AppOperation=All, or an
            // unauthenticated/system caller) - never block; this middleware only ever gates
            // a specific company's own workspace.
            await next(context);
            return;
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == companyId);
        if (company?.SaaSClientId is null)
        {
            await next(context);
            return;
        }

        var subscription = await db.TenantSubscriptions.AsNoTracking()
            .Where(item => !item.Deleted && item.CompanyId == companyId)
            .OrderByDescending(item => item.ValidTo)
            .FirstOrDefaultAsync();

        if (subscription is null || (subscription.IsActive && subscription.ValidTo >= DateTime.UtcNow))
        {
            // Never subscribed yet (trial) or currently valid - let it through either way.
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new
        {
            message = "This company's SaaS subscription has expired. Renew it to continue.",
            planName = subscription.PlanName,
            validTo = subscription.ValidTo,
            action = "Open /subscription as Admin or Owner and activate a renewal token."
        });
    }
}
