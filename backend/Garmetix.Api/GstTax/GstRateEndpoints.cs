using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Core.Models.GstTax;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>GST Rate Master CRUD plus the date-effective rate-resolution endpoint (spec section 5.6/9).</summary>
public static class GstRateEndpoints
{
    public static RouteGroupBuilder MapGstRateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst/rates")
            .WithTags("GST & Taxes - Rate Master")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapPost("/resolve", ResolveAsync);

        return group;
    }

    private static async Task EnsureStorageAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(db, loggerFactory.CreateLogger("GstTaxStorageRepair"), cancellationToken);
    }

    private static async Task<IResult> ListAsync(
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken,
        string? hsnCode = null,
        string? productCategory = null,
        bool? isActive = null)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var query = db.GstTaxRateRules.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(hsnCode))
        {
            query = query.Where(r => r.HsnCode != null && r.HsnCode.Contains(hsnCode));
        }
        if (!string.IsNullOrWhiteSpace(productCategory))
        {
            query = query.Where(r => r.ProductCategory != null && r.ProductCategory.Contains(productCategory));
        }
        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        var rows = await query.OrderBy(r => r.Priority).ThenByDescending(r => r.EffectiveFrom).ToListAsync(cancellationToken);
        return Results.Ok(rows.Select(ToRowDto));
    }

    private static async Task<IResult> CreateAsync(
        GstRateRuleSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var validation = Validate(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var entity = FromRequest(new GstTaxRateRule(), request);
        db.GstTaxRateRules.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToRowDto(entity));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        GstRateRuleSaveRequest request,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var validation = Validate(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var entity = await db.GstTaxRateRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Rate rule not found." });
        }

        FromRequest(entity, request);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToRowDto(entity));
    }

    private static async Task<IResult> DeleteAsync(Guid id, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var entity = await db.GstTaxRateRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound(new { message = "Rate rule not found." });
        }

        db.GstTaxRateRules.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { message = "Rate rule deleted." });
    }

    private static async Task<IResult> ResolveAsync(
        GstRateResolveRequest request,
        GstRateResolutionService resolver,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);
        return Results.Ok(await resolver.ResolveAsync(request, cancellationToken));
    }

    private static string? Validate(GstRateRuleSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RuleName))
        {
            return "Rule name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.HsnCode) && string.IsNullOrWhiteSpace(request.ProductCategory))
        {
            return "Provide at least an HSN code or a product category to match on.";
        }

        if (request.TaxRate < 0)
        {
            return "Tax rate cannot be negative.";
        }

        if (request.PriceThresholdFrom.HasValue && request.PriceThresholdTo.HasValue && request.PriceThresholdFrom > request.PriceThresholdTo)
        {
            return "Price threshold 'from' cannot be greater than 'to'.";
        }

        return null;
    }

    private static GstTaxRateRule FromRequest(GstTaxRateRule entity, GstRateRuleSaveRequest request)
    {
        entity.RuleName = request.RuleName.Trim();
        entity.HsnCode = string.IsNullOrWhiteSpace(request.HsnCode) ? null : request.HsnCode.Trim();
        entity.ProductCategory = string.IsNullOrWhiteSpace(request.ProductCategory) ? null : request.ProductCategory.Trim();
        entity.GoodsOrService = request.GoodsOrService;
        entity.TaxRate = request.TaxRate;
        entity.CgstRate = request.CgstRate;
        entity.SgstRate = request.SgstRate;
        entity.IgstRate = request.IgstRate;
        entity.CessRate = request.CessRate;
        entity.PriceThresholdFrom = request.PriceThresholdFrom;
        entity.PriceThresholdTo = request.PriceThresholdTo;
        entity.ThresholdBasis = request.ThresholdBasis;
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.Priority = request.Priority;
        entity.IsActive = request.IsActive;
        entity.Notes = request.Notes;
        return entity;
    }

    private static GstRateRuleRowDto ToRowDto(GstTaxRateRule r) => new(
        r.Id, r.RuleName, r.HsnCode, r.ProductCategory, r.GoodsOrService, r.TaxRate, r.CgstRate, r.SgstRate, r.IgstRate,
        r.CessRate, r.PriceThresholdFrom, r.PriceThresholdTo, r.ThresholdBasis, r.EffectiveFrom, r.EffectiveTo, r.Priority,
        r.IsActive, r.Notes);
}
