using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;

namespace Garmetix.Api.SecondarySync;

public static class OracleSecondarySyncEndpoints
{
    public static RouteGroupBuilder MapOracleSecondarySyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/oracle-sync")
            .WithTags("Oracle Secondary Sync")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);
        group.MapGet("/history", HistoryAsync);
        group.MapGet("/inbound", InboundAsync);
        group.MapGet("/ownership", OwnershipAsync);
        group.MapGet("/cloud-readiness", CloudReadinessAsync);
        group.MapGet("/auto-apply-policy", AutoApplyPolicyAsync);
        group.MapGet("/dead-letters", DeadLettersAsync);
        group.MapPost("/test", TestAsync);
        group.MapPost("/repair", RepairAsync);
        group.MapPost("/run", RunAsync);
        group.MapPost("/pull", PullAsync);
        group.MapPost("/inbound/{id:guid}/apply", ApplyInboundAsync);
        group.MapPost("/inbound/{id:guid}/reject", RejectInboundAsync);
        group.MapPost("/inbound/auto-apply", AutoApplyInboundAsync);
        group.MapPost("/dead-letters/{id:guid}/retry", RetryDeadLetterAsync);
        group.MapPost("/dead-letters/{id:guid}/resolve", ResolveDeadLetterAsync);

        return group;
    }

    private static IResult StatusAsync(OracleSecondarySyncService service)
    {
        return Results.Ok(service.GetStatus());
    }

    private static async Task<IResult> HistoryAsync(
        int? take,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetHistoryAsync(take ?? 25, cancellationToken));
    }

    private static async Task<IResult> InboundAsync(
        int? take,
        string? status,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetInboundEventsAsync(take ?? 50, status, cancellationToken));
    }

    private static IResult OwnershipAsync(OracleSecondarySyncService service)
    {
        return Results.Ok(service.GetOwnershipMatrix());
    }

    private static IResult CloudReadinessAsync(OracleSecondarySyncService service)
    {
        return Results.Ok(service.GetCloudReadiness());
    }

    private static IResult AutoApplyPolicyAsync(OracleSecondarySyncService service)
    {
        return Results.Ok(service.GetAutoApplyPolicy());
    }

    private static async Task<IResult> DeadLettersAsync(
        int? take,
        bool? includeResolved,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetDeadLettersAsync(take ?? 50, includeResolved ?? false, cancellationToken));
    }

    private static async Task<IResult> TestAsync(
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.TestConnectionAsync(cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> RepairAsync(
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.RepairAsync(cancellationToken);
            return Results.Ok(new
            {
                message = "Oracle secondary sync storage repair completed.",
                completedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or Oracle.ManagedDataAccess.Client.OracleException)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> RunAsync(
        OracleSecondarySyncRunRequest request,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RunOnceAsync(request.EntityName, request.RepairFirst, request.Direction, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> PullAsync(
        OracleSecondarySyncRunRequest request,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RunOnceAsync(request.EntityName, request.RepairFirst, "PullFromOracle", cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> ApplyInboundAsync(
        Guid id,
        OracleInboundApplyRequest request,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ApplyInboundEventAsync(id, request.Force, request.Note, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> RejectInboundAsync(
        Guid id,
        OracleInboundApplyRequest request,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RejectInboundEventAsync(id, request.Note, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }

    private static async Task<IResult> AutoApplyInboundAsync(
        OracleInboundAutoApplyRequest request,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.AutoApplyPendingInboundAsync(request.EntityName, request.Take, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> RetryDeadLetterAsync(
        Guid id,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RetryDeadLetterAsync(id, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.NotFound(result);
    }

    private static async Task<IResult> ResolveDeadLetterAsync(
        Guid id,
        OracleSecondarySyncService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ResolveDeadLetterAsync(id, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.NotFound(result);
    }
}
