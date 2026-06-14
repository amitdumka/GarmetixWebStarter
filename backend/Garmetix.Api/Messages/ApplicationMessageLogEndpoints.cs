using System.Security.Claims;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;

namespace Garmetix.Api.Messages;

public static class ApplicationMessageLogEndpoints
{
    public static RouteGroupBuilder MapApplicationMessageLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/message-logs")
            .WithTags("Message Logs")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/options", OptionsAsync);
        group.MapGet("", SearchAsync);
        group.MapPost("", CreateAsync);

        app.MapPost("/api/message-logs/client", CreateClientAsync)
            .WithTags("Message Logs")
            .RequireAuthorization();

        app.MapGet("/api/notifications", NotificationsAsync)
            .WithTags("Notifications")
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> OptionsAsync(ApplicationMessageLogService logs, CancellationToken cancellationToken)
        => Results.Ok(await logs.OptionsAsync(cancellationToken));

    private static async Task<IResult> SearchAsync(
        ApplicationMessageLogService logs,
        string? level,
        string? source,
        string? search,
        DateTime? fromUtc,
        DateTime? toUtc,
        Guid? companyId,
        Guid? storeId,
        bool? success,
        int? take,
        CancellationToken cancellationToken)
        => Results.Ok(await logs.SearchAsync(new ApplicationMessageLogQuery(level, source, search, fromUtc, toUtc, companyId, storeId, success, take ?? 100), cancellationToken));

    private static async Task<IResult> CreateAsync(
        ApplicationMessageLogCreateRequest request,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var userIdText = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value;
        var userId = Guid.TryParse(userIdText, out var parsed) ? parsed : request.UserId;

        var saved = await logs.WriteAsync(request with
        {
            UserId = request.UserId ?? userId,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? userName : request.UserName
        }, cancellationToken);

        return Results.Ok(saved);
    }

    private static async Task<IResult> CreateClientAsync(
        ClientApplicationMessageLogRequest request,
        HttpContext context,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var userId = ClaimGuid(context.User, ClaimTypes.NameIdentifier);
        var userName = context.User.Identity?.Name ?? context.User.FindFirst(ClaimTypes.Name)?.Value;
        var saved = await logs.WriteAsync(new ApplicationMessageLogCreateRequest(
            request.Level,
            "Frontend",
            request.EventName,
            request.Message,
            request.DetailsJson,
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"),
            userId,
            userName,
            request.Resource,
            null,
            request.Success), cancellationToken);

        return Results.Ok(new { saved.Id, saved.OperationId });
    }

    private static async Task<IResult> NotificationsAsync(
        HttpContext context,
        ApplicationMessageLogService logs,
        int? take,
        CancellationToken cancellationToken)
    {
        var privileged = AccessPermissionMatrix.IsAdminOrOwner(context.User);
        var items = await logs.NotificationsAsync(
            ClaimGuid(context.User, ClaimTypes.NameIdentifier),
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeId"),
            privileged,
            take ?? 12,
            cancellationToken);

        var visibleItems = items
            .Where(item => CanAccessNotification(context.User, item.ActionPath))
            .ToList();

        return Results.Ok(new ApplicationNotificationSummaryDto(visibleItems.Count, visibleItems));
    }

    private static bool CanAccessNotification(ClaimsPrincipal user, string path)
        => path switch
        {
            "/billing" or "/sales-return" or "/customers" or "/loyalty"
                => AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Billing),
            "/inventory" or "/stock-operations"
                => AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Inventory),
            "/purchase" or "/purchase-return"
                => AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Purchase),
            "/accounting" or "/petty-cash" or "/vouchers"
                => AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Accounting),
            "/hr" => AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Hr),
            "/payroll" => AccessPermissionMatrix.CanAccessPolicy(user, GarmetixPolicies.Payroll),
            "/access" or "/message-logs" => AccessPermissionMatrix.IsAdminOrOwner(user),
            _ => true
        };

    private static Guid? ClaimGuid(ClaimsPrincipal principal, string claimType)
        => Guid.TryParse(principal.FindFirst(claimType)?.Value, out var value) ? value : null;
}
