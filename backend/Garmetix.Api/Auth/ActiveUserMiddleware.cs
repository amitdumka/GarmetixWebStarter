using System.Security.Claims;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Auth;

public sealed class ActiveUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, GarmetixDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var active = await db.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.IsActive)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (!active)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "This account is inactive. Contact an Owner or Admin."
                }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
