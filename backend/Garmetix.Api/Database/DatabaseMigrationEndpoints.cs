using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Database;

public static class DatabaseMigrationEndpoints
{
    public static IEndpointRouteBuilder MapDatabaseMigrationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/database/migrations")
            .WithTags("Database")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);
        return app;
    }

    private static async Task<IResult> StatusAsync(GarmetixDbContext db, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        return Results.Ok(new DatabaseMigrationStatusDto(
            AppliedCount: applied.Length,
            PendingCount: pending.Length,
            Applied: applied,
            Pending: pending,
            AutoMigrateEnabled: configuration.GetValue<bool>("Database:AutoMigrate"),
            LastKnownConsolidatedMigration: "20260609173500_ConsolidateStage3To5Schema",
            CheckedAtUtc: DateTimeOffset.UtcNow));
    }
}

public sealed record DatabaseMigrationStatusDto(
    int AppliedCount,
    int PendingCount,
    string[] Applied,
    string[] Pending,
    bool AutoMigrateEnabled,
    string LastKnownConsolidatedMigration,
    DateTimeOffset CheckedAtUtc);
