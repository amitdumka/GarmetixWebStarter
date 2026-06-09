using Garmetix.Api.Auth;

namespace Garmetix.Api.SecondarySync;

public static class OracleSecondarySyncEndpoints
{
    public static RouteGroupBuilder MapOracleSecondarySyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/oracle-sync")
            .WithTags("Oracle Secondary Sync")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);
        group.MapPost("/test", TestAsync);
        group.MapPost("/repair", RepairAsync);
        group.MapPost("/run", RunAsync);

        return group;
    }

    private static IResult StatusAsync(OracleSecondarySyncService service)
    {
        return Results.Ok(service.GetStatus());
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
        var result = await service.RunOnceAsync(request.EntityName, request.RepairFirst, cancellationToken);
        return result.Success ? Results.Ok(result) : Results.BadRequest(result);
    }
}
