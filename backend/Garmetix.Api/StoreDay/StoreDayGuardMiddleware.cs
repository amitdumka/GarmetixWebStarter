
using System.Security.Claims;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Garmetix.Api.Workspace;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.StoreDay;

public sealed class StoreDayGuardMiddleware(RequestDelegate next)
{
    private static readonly string[] GuardedPrefixes =
    [
        "/api/billing",
        "/api/sales-invoices",
        "/api/sales-return",
        "/api/tailoring",
        "/api/purchase",
        "/api/purchase-invoices",
        "/api/vendor-payments",
        "/api/stock-operations",
        "/api/stocks",
        "/api/vouchers",
        "/api/cash-vouchers",
        "/api/petty-cash-sheets",
        "/api/customers",
        "/api/parties"
    ];

    public async Task InvokeAsync(HttpContext context, GarmetixDbContext db)
    {
        if (!ShouldGuard(context))
        {
            await next(context);
            return;
        }

        var storeId = WorkspaceScope.ClaimGuid(context, "storeId");
        if (!storeId.HasValue || storeId.Value == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Store day control requires an assigned store for Store Manager/Billing users."
            });
            return;
        }

        var today = DateTime.Today;
        var opened = await db.DayBegins.AsNoTracking()
            .AnyAsync(item => item.StoreId == storeId.Value && item.OnDate == today && !item.Deleted, context.RequestAborted);
        var closed = await db.DayEnds.AsNoTracking()
            .AnyAsync(item => item.StoreId == storeId.Value && item.OnDate == today && !item.Deleted, context.RequestAborted);

        if (!opened || closed)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new
            {
                message = !opened
                    ? "Store day opening is required before daily entries."
                    : "Store day is already closed. Daily entries are locked.",
                storeId,
                onDate = today,
                openRequired = !opened,
                closed
            });
            return;
        }

        await next(context);
    }

    private static bool ShouldGuard(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (WorkspaceScope.HasFullAccess(context) || IsAccountant(context.User))
        {
            return false;
        }

        if (!IsStoreManagerOrBiller(context.User))
        {
            return false;
        }

        if (!HttpMethods.IsPost(context.Request.Method)
            && !HttpMethods.IsPut(context.Request.Method)
            && !HttpMethods.IsDelete(context.Request.Method)
            && !HttpMethods.IsPatch(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWith("/api/store-day", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/workspace", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/message-logs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return GuardedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStoreManagerOrBiller(ClaimsPrincipal user)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? string.Empty;
        var userType = user.FindFirst("userType")?.Value ?? string.Empty;
        return Equals(role, LoginRole.StoreManager.ToString())
            || Equals(role, LoginRole.Salesman.ToString())
            || Equals(userType, UserType.StoreManager.ToString())
            || Equals(userType, UserType.Sales.ToString());
    }

    private static bool IsAccountant(ClaimsPrincipal user)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value ?? string.Empty;
        var userType = user.FindFirst("userType")?.Value ?? string.Empty;
        return Equals(role, LoginRole.Accountant.ToString())
            || Equals(role, LoginRole.RemoteAccountant.ToString())
            || Equals(userType, UserType.Accountant.ToString())
            || Equals(userType, UserType.CA.ToString());
    }

    private static bool Equals(string value, string expected)
        => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
