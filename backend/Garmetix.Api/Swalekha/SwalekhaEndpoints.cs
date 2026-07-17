using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;

namespace Garmetix.Api.Swalekha;

/// <summary>
/// Foundation-stage endpoints for the isolated Swalekha (Personal &amp; Personal Finance)
/// module. Every route here requires GarmetixPolicies.SwalekhaOwner - Owner userType only,
/// no SuperAdmin/Admin fallback. Real domain endpoints arrive in PersonalFin_02 onward.
/// </summary>
public static class SwalekhaEndpoints
{
    public static RouteGroupBuilder MapSwalekhaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha")
            .WithTags("Swalekha")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/health", HealthAsync);

        return group;
    }

    private static async Task<IResult> HealthAsync(
        SwalekhaDbContext db,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        bool connected;
        try
        {
            connected = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            connected = false;
        }

        var ownerName = http.User.Identity?.Name ?? "Owner";

        return Results.Ok(new
        {
            ok = connected,
            database = connected ? "connected" : "unreachable",
            ownerName,
            generatedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
