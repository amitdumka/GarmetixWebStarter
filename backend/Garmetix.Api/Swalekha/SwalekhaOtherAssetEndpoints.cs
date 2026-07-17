using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaOtherAssetDto(
    Guid Id,
    string AssetType,
    string Name,
    decimal CurrentValue,
    DateTime AsOfDate,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaOtherAssetPayload(
    string AssetType,
    string Name,
    decimal CurrentValue,
    DateTime AsOfDate,
    bool IsActive,
    string? Notes);

/// <summary>
/// PersonalFin_10 - simple asset snapshots for PPF/EPF/NPS/Gold and similar holdings, scoped
/// deliberately as CurrentValue-only entries (no transaction history, no account integration) -
/// the design explicitly called these "simple asset entries," distinct from Shares/Mutual Funds'
/// full buy-sell tracking. The Owner updates CurrentValue periodically as statements arrive.
/// </summary>
public static class SwalekhaOtherAssetEndpoints
{
    public static RouteGroupBuilder MapSwalekhaOtherAssetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/other-assets")
            .WithTags("Swalekha Investments")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListAssetsAsync);
        group.MapGet("/{id:guid}", GetAssetAsync);
        group.MapPost("/", CreateAssetAsync);
        group.MapPut("/{id:guid}", UpdateAssetAsync);
        group.MapDelete("/{id:guid}", DeleteAssetAsync);

        return group;
    }

    private static async Task<IResult> ListAssetsAsync(SwalekhaDbContext db, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaOtherAssets.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(a => a.IsActive);
        }

        var assets = await query.OrderBy(a => a.Name).ToListAsync(cancellationToken);
        return Results.Ok(assets.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetAssetAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var asset = await db.SwalekhaOtherAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return asset is null ? Results.NotFound() : Results.Ok(ToDto(asset));
    }

    private static async Task<IResult> CreateAssetAsync(SwalekhaOtherAssetPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Name is required." });
        }

        if (!Enum.TryParse<SwalekhaOtherAssetType>(payload.AssetType, true, out var assetType))
        {
            return Results.BadRequest(new { message = $"Unknown asset type '{payload.AssetType}'." });
        }

        var asset = new SwalekhaOtherAsset
        {
            AssetType = assetType,
            Name = payload.Name.Trim(),
            CurrentValue = payload.CurrentValue,
            AsOfDate = payload.AsOfDate,
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaOtherAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/other-assets/{asset.Id}", ToDto(asset));
    }

    private static async Task<IResult> UpdateAssetAsync(Guid id, SwalekhaOtherAssetPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var asset = await db.SwalekhaOtherAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return Results.BadRequest(new { message = "Name is required." });
        }

        if (!Enum.TryParse<SwalekhaOtherAssetType>(payload.AssetType, true, out var assetType))
        {
            return Results.BadRequest(new { message = $"Unknown asset type '{payload.AssetType}'." });
        }

        asset.AssetType = assetType;
        asset.Name = payload.Name.Trim();
        asset.CurrentValue = payload.CurrentValue;
        asset.AsOfDate = payload.AsOfDate;
        asset.IsActive = payload.IsActive;
        asset.Notes = payload.Notes;
        asset.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(asset));
    }

    private static async Task<IResult> DeleteAssetAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var asset = await db.SwalekhaOtherAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null) return Results.NotFound();

        asset.Deleted = true;
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static SwalekhaOtherAssetDto ToDto(SwalekhaOtherAsset asset) => new(
        asset.Id,
        asset.AssetType.ToString(),
        asset.Name,
        asset.CurrentValue,
        asset.AsOfDate,
        asset.IsActive,
        asset.Notes,
        asset.CreatedAt);
}
