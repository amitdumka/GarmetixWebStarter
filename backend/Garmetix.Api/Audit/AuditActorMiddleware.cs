using System.Security.Claims;
using Garmetix.Infrastructure.Audit;

namespace Garmetix.Api.Audit;

public sealed class AuditActorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuditActorContext auditActor)
    {
        auditActor.RequestMethod = context.Request.Method;
        auditActor.RequestPath = context.Request.Path.Value;
        auditActor.IpAddress = context.Connection.RemoteIpAddress?.ToString();
        auditActor.TraceIdentifier = context.TraceIdentifier;

        if (context.User.Identity?.IsAuthenticated == true)
        {
            auditActor.UserName = context.User.FindFirstValue(ClaimTypes.Name) ?? context.User.Identity.Name;
            auditActor.UserId = ReadGuidClaim(context, ClaimTypes.NameIdentifier);
            auditActor.CompanyId = ReadGuidClaim(context, "companyId");
            auditActor.StoreGroupId = ReadGuidClaim(context, "storeGroupId");
            auditActor.StoreId = ReadGuidClaim(context, "storeId");
        }

        await next(context);
    }

    private static Guid? ReadGuidClaim(HttpContext context, string claimType)
        => Guid.TryParse(context.User.FindFirstValue(claimType), out var value) ? value : null;
}
