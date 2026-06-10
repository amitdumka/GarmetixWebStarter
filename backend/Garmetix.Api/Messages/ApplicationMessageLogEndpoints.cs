using System.Security.Claims;
using Garmetix.Api.Auth;

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
        group.MapGet("/", SearchAsync);
        group.MapPost("", CreateAsync);
        group.MapPost("/", CreateAsync);

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
}
