using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.Setup;

public static class MasterDataEndpoints
{
    public static RouteGroupBuilder MapMasterDataEndpoints(this WebApplication app)
    {
        var vendors = app.MapGroup("/api/vendors").WithTags("Vendors").RequireAuthorization(GarmetixPolicies.Purchase);
        vendors.MapGet("/", GetVendorsAsync);
        vendors.MapPost("/", CreateVendorAsync).RequireAuthorization(GarmetixPolicies.Edit);
        vendors.MapPut("/{id:guid}", UpdateVendorAsync).RequireAuthorization(GarmetixPolicies.Edit);
        vendors.MapDelete("/{id:guid}", DeleteVendorAsync).RequireAuthorization(GarmetixPolicies.Delete);

        var masters = app.MapGroup("/api/masters").WithTags("Inventory Masters").RequireAuthorization(GarmetixPolicies.Inventory);
        masters.MapGet("/brands", GetBrandsAsync);
        masters.MapPost("/brands", CreateBrandAsync).RequireAuthorization(GarmetixPolicies.Edit);
        masters.MapPut("/brands/{id:guid}", UpdateBrandAsync).RequireAuthorization(GarmetixPolicies.Edit);
        masters.MapDelete("/brands/{id:guid}", DeleteBrandAsync).RequireAuthorization(GarmetixPolicies.Delete);

        masters.MapGet("/product-categories", GetCategoriesAsync);
        masters.MapPost("/product-categories", CreateCategoryAsync).RequireAuthorization(GarmetixPolicies.Edit);
        masters.MapPut("/product-categories/{id:guid}", UpdateCategoryAsync).RequireAuthorization(GarmetixPolicies.Edit);
        masters.MapDelete("/product-categories/{id:guid}", DeleteCategoryAsync).RequireAuthorization(GarmetixPolicies.Delete);

        masters.MapGet("/product-subcategories", GetSubCategoriesAsync);
        masters.MapPost("/product-subcategories", CreateSubCategoryAsync).RequireAuthorization(GarmetixPolicies.Edit);
        masters.MapPut("/product-subcategories/{id:guid}", UpdateSubCategoryAsync).RequireAuthorization(GarmetixPolicies.Edit);
        masters.MapDelete("/product-subcategories/{id:guid}", DeleteSubCategoryAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return app.MapGroup("/api");
    }

    private static async Task<IReadOnlyList<VendorDto>> GetVendorsAsync(HttpContext context, GarmetixDbContext db, string? q = null, bool includeInactive = true, bool includeTailoringVendors = false, CancellationToken cancellationToken = default)
    {
        var term = q?.Trim().ToLowerInvariant();
        var query = WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context);
        if (!includeInactive)
        {
            query = query.Where(item => item.Active);
        }
        if (!includeTailoringVendors)
        {
            // Tailoring/alteration vendors are a separate vendor pool managed from the
            // Tailoring module (see TailoringEndpoints.cs); keep them out of the general
            // purchase vendor list unless explicitly requested.
            query = query.Where(item => item.VendorType == null || item.VendorType != VendorType.Tailoring);
        }
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item => item.Name.ToLower().Contains(term) || item.MobileNumber.ToLower().Contains(term) || (item.GSTIN != null && item.GSTIN.ToLower().Contains(term)) || (item.City != null && item.City.ToLower().Contains(term)));
        }
        return await query.OrderBy(item => item.Name).Select(item => new VendorDto(
            item.Id, item.CompanyId, item.Name, item.Address, item.City, item.ZipCode, item.MobileNumber, item.Email, item.GSTIN,
            item.Pan, item.Tan, item.Active, item.BillCount, item.BillAmount, item.Paid, item.BillAmount - item.Paid, (int?)item.VendorType)).ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateVendorAsync(VendorWriteDto request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Company and vendor name are required." });
        var vendor = new Vendor
        {
            CompanyId = request.CompanyId,
            Name = request.Name.Trim(),
            Address = RequiredOrDefault(request.Address, ""),
            City = RequiredOrDefault(request.City, "Dumka"),
            ZipCode = NullIfEmpty(request.ZipCode),
            MobileNumber = RequiredOrDefault(request.MobileNumber, "0000000000"),
            Email = NullIfEmpty(request.Email),
            GSTIN = NormalizeUpper(request.GSTIN),
            Pan = NormalizeUpper(request.Pan),
            Tan = NormalizeUpper(request.Tan),
            Active = request.Active ?? true,
            VendorType = request.VendorType.HasValue ? (VendorType)request.VendorType.Value : null,
            StartDate = DateTime.Today
        };
        if (!WorkspaceScope.CanWrite(vendor, context, out var message)) return Results.BadRequest(new { message = message ?? "Selected company is outside your access scope." });
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/vendors/{vendor.Id}", new { vendor.Id });
    }

    private static async Task<IResult> UpdateVendorAsync(Guid id, VendorWriteDto request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var vendor = await WorkspaceScope.ApplyTo(db.Vendors, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (vendor is null) return Results.NotFound(new { message = "Vendor not found." });
        vendor.Name = RequiredOrDefault(request.Name, vendor.Name);
        vendor.Address = RequiredOrDefault(request.Address, vendor.Address);
        vendor.City = RequiredOrDefault(request.City, vendor.City);
        vendor.ZipCode = NullIfEmpty(request.ZipCode);
        vendor.MobileNumber = RequiredOrDefault(request.MobileNumber, vendor.MobileNumber);
        vendor.Email = NullIfEmpty(request.Email);
        vendor.GSTIN = NormalizeUpper(request.GSTIN);
        vendor.Pan = NormalizeUpper(request.Pan);
        vendor.Tan = NormalizeUpper(request.Tan);
        vendor.Active = request.Active ?? vendor.Active;
        vendor.VendorType = request.VendorType.HasValue ? (VendorType)request.VendorType.Value : vendor.VendorType;
        vendor.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { vendor.Id });
    }

    private static async Task<IResult> DeleteVendorAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var vendor = await WorkspaceScope.ApplyTo(db.Vendors, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (vendor is null) return Results.NotFound(new { message = "Vendor not found." });
        var hasInvoices = await db.PurchaseInvoices.AnyAsync(item => item.VendorId == id, cancellationToken);
        if (hasInvoices)
        {
            vendor.Active = false;
            vendor.Deleted = true;
            vendor.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.Vendors.Remove(vendor);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(GarmetixDbContext db, string? q = null, CancellationToken cancellationToken = default)
    {
        var term = q?.Trim().ToLowerInvariant();
        var query = db.Brands.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Name.ToLower().Contains(term) || item.BrandCode.ToLower().Contains(term));
        return await query.OrderBy(item => item.Name).Select(item => new BrandDto(item.Id, item.Name, item.BrandCode, item.SupplierId)).ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateBrandAsync(BrandWriteDto request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Brand name is required." });
        var brand = new Brand { Name = request.Name.Trim(), BrandCode = RequiredOrDefault(request.BrandCode, MakeCode(request.Name)), SupplierId = request.SupplierId };
        db.Brands.Add(brand);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/masters/brands/{brand.Id}", new { brand.Id });
    }

    private static async Task<IResult> UpdateBrandAsync(Guid id, BrandWriteDto request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (brand is null) return Results.NotFound(new { message = "Brand not found." });
        brand.Name = RequiredOrDefault(request.Name, brand.Name);
        brand.BrandCode = RequiredOrDefault(request.BrandCode, brand.BrandCode);
        brand.SupplierId = request.SupplierId;
        brand.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { brand.Id });
    }

    private static async Task<IResult> DeleteBrandAsync(Guid id, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (brand is null) return Results.NotFound(new { message = "Brand not found." });
        var used = await db.ProductDetails.AnyAsync(item => item.Brand == brand.Name, cancellationToken);
        if (used)
        {
            brand.Deleted = true;
            brand.UpdatedAt = DateTime.UtcNow;
        }
        else db.Brands.Remove(brand);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(HttpContext context, GarmetixDbContext db, string? q = null, CancellationToken cancellationToken = default)
    {
        var term = q?.Trim().ToLowerInvariant();
        var query = WorkspaceScope.ApplyTo(db.ProductCategories.AsNoTracking(), context);
        if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Name.ToLower().Contains(term));
        return await query.OrderBy(item => item.ProductGroup).ThenBy(item => item.Name).Select(item => new CategoryDto(item.Id, item.CompanyId, item.Name, item.ProductGroup, item.ProductGroup.ToString() ?? "-", item.IsActive)).ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateCategoryAsync(CategoryWriteDto request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Company and category name are required." });
        var category = new InventoryProductCategory { CompanyId = request.CompanyId, Name = request.Name.Trim(), ProductGroup = request.ProductGroup, IsActive = request.IsActive ?? true };
        if (!WorkspaceScope.CanWrite(category, context, out var message)) return Results.BadRequest(new { message = message ?? "Selected company is outside your access scope." });
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/masters/product-categories/{category.Id}", new { category.Id });
    }

    private static async Task<IResult> UpdateCategoryAsync(Guid id, CategoryWriteDto request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var category = await WorkspaceScope.ApplyTo(db.ProductCategories, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null) return Results.NotFound(new { message = "Category not found." });
        category.Name = RequiredOrDefault(request.Name, category.Name);
        category.ProductGroup = request.ProductGroup ?? category.ProductGroup;
        category.IsActive = request.IsActive ?? category.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { category.Id });
    }

    private static async Task<IResult> DeleteCategoryAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var category = await WorkspaceScope.ApplyTo(db.ProductCategories, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null) return Results.NotFound(new { message = "Category not found." });
        var used = await db.Products.AnyAsync(item => item.ProductCategoryId == id, cancellationToken);
        if (used)
        {
            category.IsActive = false;
            category.Deleted = true;
            category.UpdatedAt = DateTime.UtcNow;
        }
        else db.ProductCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<SubCategoryDto>> GetSubCategoriesAsync(HttpContext context, GarmetixDbContext db, string? q = null, Guid? categoryId = null, CancellationToken cancellationToken = default)
    {
        var term = q?.Trim().ToLowerInvariant();
        var query = WorkspaceScope.ApplyTo(db.ProductSubCategories.AsNoTracking(), context);
        if (categoryId.HasValue) query = query.Where(item => item.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(term)) query = query.Where(item => item.Name.ToLower().Contains(term));
        return await query.OrderBy(item => item.Name).Select(item => new SubCategoryDto(item.Id, item.CompanyId, item.Name, item.CategoryId)).ToListAsync(cancellationToken);
    }

    private static async Task<IResult> CreateSubCategoryAsync(SubCategoryWriteDto request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { message = "Company and sub-category name are required." });
        var subCategory = new InventoryProductSubCategory { CompanyId = request.CompanyId, Name = request.Name.Trim(), CategoryId = request.CategoryId };
        if (!WorkspaceScope.CanWrite(subCategory, context, out var message)) return Results.BadRequest(new { message = message ?? "Selected company is outside your access scope." });
        db.ProductSubCategories.Add(subCategory);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/masters/product-subcategories/{subCategory.Id}", new { subCategory.Id });
    }

    private static async Task<IResult> UpdateSubCategoryAsync(Guid id, SubCategoryWriteDto request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var subCategory = await WorkspaceScope.ApplyTo(db.ProductSubCategories, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subCategory is null) return Results.NotFound(new { message = "Sub-category not found." });
        subCategory.Name = RequiredOrDefault(request.Name, subCategory.Name);
        subCategory.CategoryId = request.CategoryId;
        subCategory.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { subCategory.Id });
    }

    private static async Task<IResult> DeleteSubCategoryAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var subCategory = await WorkspaceScope.ApplyTo(db.ProductSubCategories, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subCategory is null) return Results.NotFound(new { message = "Sub-category not found." });
        var used = await db.Products.AnyAsync(item => item.ProductSubCategoryId == id, cancellationToken);
        if (used)
        {
            subCategory.Deleted = true;
            subCategory.UpdatedAt = DateTime.UtcNow;
        }
        else db.ProductSubCategories.Remove(subCategory);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeUpper(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string RequiredOrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string MakeCode(string value) => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Take(8).ToArray()).ToUpperInvariant();
}

public sealed record VendorDto(Guid Id, Guid CompanyId, string Name, string Address, string City, string? ZipCode, string MobileNumber, string? Email, string? GSTIN, string? Pan, string? Tan, bool Active, int BillCount, decimal BillAmount, decimal PaidAmount, decimal BalanceAmount, int? VendorType);
public sealed record VendorWriteDto(Guid CompanyId, string Name, string? Address, string? City, string? ZipCode, string? MobileNumber, string? Email, string? GSTIN, string? Pan, string? Tan, bool? Active, int? VendorType = null);
public sealed record BrandDto(Guid Id, string Name, string BrandCode, Guid? SupplierId);
public sealed record BrandWriteDto(string Name, string? BrandCode, Guid? SupplierId);
public sealed record CategoryDto(Guid Id, Guid CompanyId, string Name, ProductGroup? ProductGroup, string ProductGroupName, bool IsActive);
public sealed record CategoryWriteDto(Guid CompanyId, string Name, ProductGroup? ProductGroup, bool? IsActive);
public sealed record SubCategoryDto(Guid Id, Guid CompanyId, string Name, Guid? CategoryId);
public sealed record SubCategoryWriteDto(Guid CompanyId, string Name, Guid? CategoryId);
