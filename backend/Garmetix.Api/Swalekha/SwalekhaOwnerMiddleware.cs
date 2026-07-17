using System.Security.Claims;
using Garmetix.Infrastructure.Data;

namespace Garmetix.Api.Swalekha;

/// <summary>
/// PersonalFin_06 - populates the scoped SwalekhaOwnerContext from the authenticated user's
/// NameIdentifier claim (the same AppUser.Id every JWT already carries as "sub"), before
/// SwalekhaDbContext is ever resolved for the request. Same pattern as AuditActorMiddleware/
/// AuditActorContext. Runs globally (cheap, harmless for non-Swalekha requests) rather than
/// only for /api/swalekha - simpler than route-conditional middleware registration.
/// </summary>
public sealed class SwalekhaOwnerMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SwalekhaOwnerContext ownerContext)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
        {
            ownerContext.OwnerId = ownerId;
        }

        await next(context);
    }
}
