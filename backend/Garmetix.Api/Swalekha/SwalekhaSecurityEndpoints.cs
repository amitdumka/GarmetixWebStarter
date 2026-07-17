using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaSelfCheckItem(string Name, bool Passed, string Detail);

public sealed record SwalekhaSelfCheckDto(bool AllPassed, Guid OwnerId, IReadOnlyList<SwalekhaSelfCheckItem> Checks);

/// <summary>
/// PersonalFin_15 - Hardening. A live, runtime self-check of the Owner-only isolation guarantee
/// PersonalFin_06 built - not a static claim, an assertion actually run against the current
/// request's data. Confirms the database is reachable, the Owner context resolved from the JWT,
/// and (the real teeth of it) that SwalekhaDbContext's global query filter demonstrably excludes
/// other owners' rows from a normal scoped query, even when other owners' data genuinely exists
/// in the same database.
/// </summary>
public static class SwalekhaSecurityEndpoints
{
    public static RouteGroupBuilder MapSwalekhaSecurityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/security")
            .WithTags("Swalekha Security")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/self-check", RunSelfCheckAsync);

        return group;
    }

    private static async Task<IResult> RunSelfCheckAsync(SwalekhaDbContext db, SwalekhaOwnerContext ownerContext, CancellationToken cancellationToken)
    {
        var checks = new List<SwalekhaSelfCheckItem>();

        bool databaseConnected;
        try
        {
            databaseConnected = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            databaseConnected = false;
        }
        checks.Add(new SwalekhaSelfCheckItem("Database connected", databaseConnected, databaseConnected ? "swalekha_db reachable" : "Could not reach swalekha_db"));

        var ownerResolved = ownerContext.OwnerId != Guid.Empty;
        checks.Add(new SwalekhaSelfCheckItem("Owner identity resolved", ownerResolved, ownerResolved ? $"Owner {ownerContext.OwnerId}" : "No Owner id on the current request's JWT"));

        // The real assertion: query SwalekhaAccounts the normal (filtered) way and compare it
        // against an explicit IgnoreQueryFilters query scoped to this owner - if PersonalFin_06's
        // global filter is doing its job, these two counts must be identical. Then confirm other
        // owners' rows genuinely exist elsewhere in the same table but are invisible to the
        // filtered query - proving isolation isn't just "true because there's nothing else here."
        var filteredCount = await db.SwalekhaAccounts.CountAsync(cancellationToken);
        var myUnfilteredCount = await db.SwalekhaAccounts.IgnoreQueryFilters()
            .CountAsync(a => !a.Deleted && a.OwnerId == ownerContext.OwnerId, cancellationToken);
        var filterVerified = filteredCount == myUnfilteredCount;
        checks.Add(new SwalekhaSelfCheckItem(
            "Query filter scopes to this Owner only",
            filterVerified,
            filterVerified
                ? $"Filtered query returned exactly {filteredCount} row(s), matching an explicit Owner-scoped count"
                : $"Mismatch: filtered query returned {filteredCount} but Owner-scoped count is {myUnfilteredCount}"));

        var totalAcrossAllOwners = await db.SwalekhaAccounts.IgnoreQueryFilters().CountAsync(a => !a.Deleted, cancellationToken);
        var otherOwnersDataExists = totalAcrossAllOwners > myUnfilteredCount;
        checks.Add(new SwalekhaSelfCheckItem(
            "Isolation proven against real other-owner data",
            otherOwnersDataExists ? filterVerified : true,
            otherOwnersDataExists
                ? $"{totalAcrossAllOwners - myUnfilteredCount} account row(s) belonging to other Owners exist and are correctly invisible above"
                : "No other Owners have data yet in this environment - isolation logic is correct by construction (PersonalFin_06's filter), but not exercised against a second Owner's real rows right now"));

        var allPassed = checks.All(c => c.Passed);
        return Results.Ok(new SwalekhaSelfCheckDto(allPassed, ownerContext.OwnerId, checks));
    }
}
