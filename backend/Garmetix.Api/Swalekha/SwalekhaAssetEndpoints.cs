using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaAssetDto(
    Guid Id,
    string Category,
    string AssetSubType,
    string Name,
    decimal? PurchaseValue,
    DateTime? PurchaseDate,
    decimal CurrentValue,
    DateTime AsOfDate,
    string? Location,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaAssetPayload(
    string Category,
    string AssetSubType,
    string Name,
    decimal? PurchaseValue,
    DateTime? PurchaseDate,
    decimal CurrentValue,
    DateTime AsOfDate,
    string? Location,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaAssetSummaryDto(
    decimal ImmovableTotal,
    int ImmovableCount,
    decimal MovableTotal,
    int MovableCount,
    decimal GrandTotal);

/// <summary>
/// General personal property register (house/flat/land as Immovable; gold/vehicles/luxury
/// items/electronics as Movable) - see SwalekhaAssets.cs for how this differs from
/// PersonalFin_10's narrower SwalekhaOtherAsset (PPF/EPF/NPS/Gold-as-investment).
/// </summary>
public static class SwalekhaAssetEndpoints
{
    public static RouteGroupBuilder MapSwalekhaAssetEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/assets")
            .WithTags("Swalekha Assets")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListAssetsAsync);
        group.MapGet("/summary", GetSummaryAsync);
        group.MapGet("/{id:guid}", GetAssetAsync);
        group.MapPost("/", CreateAssetAsync);
        group.MapPut("/{id:guid}", UpdateAssetAsync);
        group.MapDelete("/{id:guid}", DeleteAssetAsync);

        return group;
    }

    private static async Task<IResult> ListAssetsAsync(SwalekhaDbContext db, string? category, bool? includeInactive, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaAssets.AsNoTracking().AsQueryable();
        if (includeInactive != true)
        {
            query = query.Where(a => a.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<SwalekhaAssetCategory>(category, true, out var categoryFilter))
        {
            query = query.Where(a => a.Category == categoryFilter);
        }

        var assets = await query.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync(cancellationToken);
        return Results.Ok(assets.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetSummaryAsync(SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var active = await db.SwalekhaAssets.AsNoTracking().Where(a => a.IsActive).ToListAsync(cancellationToken);
        var immovable = active.Where(a => a.Category == SwalekhaAssetCategory.Immovable).ToList();
        var movable = active.Where(a => a.Category == SwalekhaAssetCategory.Movable).ToList();

        return Results.Ok(new SwalekhaAssetSummaryDto(
            ImmovableTotal: immovable.Sum(a => a.CurrentValue),
            ImmovableCount: immovable.Count,
            MovableTotal: movable.Sum(a => a.CurrentValue),
            MovableCount: movable.Count,
            GrandTotal: active.Sum(a => a.CurrentValue)));
    }

    private static async Task<IResult> GetAssetAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var asset = await db.SwalekhaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return asset is null ? Results.NotFound() : Results.Ok(ToDto(asset));
    }

    private static async Task<IResult> CreateAssetAsync(SwalekhaAssetPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var validationError = Validate(payload, out var category);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        var asset = new SwalekhaAsset
        {
            Category = category,
            AssetSubType = payload.AssetSubType.Trim(),
            Name = payload.Name.Trim(),
            PurchaseValue = payload.PurchaseValue,
            PurchaseDate = payload.PurchaseDate,
            CurrentValue = payload.CurrentValue,
            AsOfDate = payload.AsOfDate,
            Location = string.IsNullOrWhiteSpace(payload.Location) ? null : payload.Location.Trim(),
            IsActive = payload.IsActive,
            Notes = payload.Notes
        };

        db.SwalekhaAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/swalekha/assets/{asset.Id}", ToDto(asset));
    }

    private static async Task<IResult> UpdateAssetAsync(Guid id, SwalekhaAssetPayload payload, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var asset = await db.SwalekhaAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null) return Results.NotFound();

        var validationError = Validate(payload, out var category);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        asset.Category = category;
        asset.AssetSubType = payload.AssetSubType.Trim();
        asset.Name = payload.Name.Trim();
        asset.PurchaseValue = payload.PurchaseValue;
        asset.PurchaseDate = payload.PurchaseDate;
        asset.CurrentValue = payload.CurrentValue;
        asset.AsOfDate = payload.AsOfDate;
        asset.Location = string.IsNullOrWhiteSpace(payload.Location) ? null : payload.Location.Trim();
        asset.IsActive = payload.IsActive;
        asset.Notes = payload.Notes;
        asset.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToDto(asset));
    }

    private static async Task<IResult> DeleteAssetAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var asset = await db.SwalekhaAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null) return Results.NotFound();

        asset.Deleted = true;
        asset.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static string? Validate(SwalekhaAssetPayload payload, out SwalekhaAssetCategory category)
    {
        category = default;

        if (string.IsNullOrWhiteSpace(payload.Name))
        {
            return "Name is required.";
        }

        if (string.IsNullOrWhiteSpace(payload.AssetSubType))
        {
            return "Type is required (e.g. House, Flat, Land, Gold, Car, Watch, Mobile, Laptop).";
        }

        if (!Enum.TryParse(payload.Category, true, out category))
        {
            return $"Category must be Immovable or Movable, got '{payload.Category}'.";
        }

        if (payload.CurrentValue < 0 || payload.PurchaseValue < 0)
        {
            return "Values cannot be negative.";
        }

        return null;
    }

    private static SwalekhaAssetDto ToDto(SwalekhaAsset asset) => new(
        asset.Id,
        asset.Category.ToString(),
        asset.AssetSubType,
        asset.Name,
        asset.PurchaseValue,
        asset.PurchaseDate,
        asset.CurrentValue,
        asset.AsOfDate,
        asset.Location,
        asset.IsActive,
        asset.Notes,
        asset.CreatedAt);
}
