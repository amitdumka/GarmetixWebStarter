using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.Setup;

public static class SetupEndpoints
{
    public static RouteGroupBuilder MapSetupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/setup")
            .WithTags("Setup")
            .RequireAuthorization();

        group.MapGet("/status", GetStatusAsync);
        group.MapPost("/quick-start", QuickStartAsync).RequireAuthorization(GarmetixPolicies.CompanySetup);
        group.MapPost("/quick-product", QuickProductAsync).RequireAuthorization(GarmetixPolicies.Inventory);

        return group;
    }

    private static async Task<SetupStatusResponse> GetStatusAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.AsNoTracking().OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var storeGroup = await db.StoreGroups.AsNoTracking().OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        var store = await db.Stores.AsNoTracking().OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);

        return new SetupStatusResponse(
            company is not null,
            storeGroup is not null,
            store is not null,
            await db.ProductCategories.AnyAsync(cancellationToken),
            await db.Taxes.AnyAsync(cancellationToken),
            company?.Id,
            storeGroup?.Id,
            store?.Id);
    }

    private static async Task<IResult> QuickStartAsync(QuickSetupRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FirstOrDefaultAsync(cancellationToken);
        if (company is null)
        {
            company = new Company
            {
                Name = RequiredOrDefault(request.CompanyName, "Garmetix Company"),
                Code = "MAIN",
                Active = true,
                ContactNumber = request.ContactNumber ?? string.Empty,
                Email = request.Email ?? string.Empty,
                Address = RequiredOrDefault(request.City, "Dumka"),
                City = RequiredOrDefault(request.City, "Dumka"),
                State = RequiredOrDefault(request.State, "Jharkhand"),
                Country = "India",
                ZipCode = RequiredOrDefault(request.ZipCode, "814101"),
                ContactPerson = "Admin",
                ContactMobile = request.ContactNumber ?? string.Empty
            };
            db.Companies.Add(company);
        }

        var storeGroup = await db.StoreGroups.FirstOrDefaultAsync(item => item.CompanyId == company.Id, cancellationToken);
        if (storeGroup is null)
        {
            storeGroup = new StoreGroup
            {
                Name = RequiredOrDefault(request.StoreGroupName, "Main Group"),
                GroupCode = "MAIN",
                Active = true,
                CompanyId = company.Id
            };
            db.StoreGroups.Add(storeGroup);
        }

        var store = await db.Stores.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.StoreGroupId == storeGroup.Id, cancellationToken);
        if (store is null)
        {
            store = new Store
            {
                Name = RequiredOrDefault(request.StoreName, "Main Store"),
                StoreCode = "MAIN",
                Active = true,
                CompanyId = company.Id,
                StoreGroupId = storeGroup.Id,
                ContactNumber = request.ContactNumber ?? string.Empty,
                Email = request.Email ?? string.Empty,
                Address = RequiredOrDefault(request.City, "Dumka"),
                City = RequiredOrDefault(request.City, "Dumka"),
                State = RequiredOrDefault(request.State, "Jharkhand"),
                Country = "India",
                ZipCode = RequiredOrDefault(request.ZipCode, "814101")
            };
            db.Stores.Add(store);
        }

        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == "General", cancellationToken);
        if (category is null)
        {
            category = new InventoryProductCategory
            {
                Name = "General",
                CompanyId = company.Id
            };
            db.ProductCategories.Add(category);
        }

        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == company.Id && item.Name == "General", cancellationToken);
        if (subCategory is null)
        {
            subCategory = new InventoryProductSubCategory
            {
                Name = "General",
                CompanyId = company.Id
            };
            db.ProductSubCategories.Add(subCategory);
        }

        var tax = await db.Taxes.FirstOrDefaultAsync(item => item.Name == "GST 5", cancellationToken);
        if (tax is null)
        {
            tax = new Tax
            {
                Name = "GST 5",
                CompositeRate = 5,
                TaxType = TaxType.GST
            };
            db.Taxes.Add(tax);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new QuickSetupResponse(company.Id, storeGroup.Id, store.Id, category.Id, subCategory.Id, tax.Id));
    }

    private static async Task<IResult> QuickProductAsync(QuickProductRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Barcode))
        {
            return Results.BadRequest(new { message = "Product name and barcode are required." });
        }

        var categoryId = request.ProductCategoryId ?? await db.ProductCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var subCategoryId = request.ProductSubCategoryId ?? await db.ProductSubCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var tax = request.TaxId.HasValue
            ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == request.TaxId.Value, cancellationToken)
            : await db.Taxes.FirstOrDefaultAsync(cancellationToken);

        if (categoryId == Guid.Empty || subCategoryId == Guid.Empty || tax is null)
        {
            return Results.BadRequest(new { message = "Run quick setup before adding products." });
        }

        var product = new Product
        {
            Name = request.Name.Trim(),
            Barcode = request.Barcode.Trim(),
            MRP = request.Mrp,
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            Unit = Unit.Pcs,
            ProductType = ProductType.Apparels,
            ProductCategoryId = categoryId,
            ProductSubCategoryId = subCategoryId,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId
        };

        var stock = new Stock
        {
            ProductId = product.Id,
            Barcode = product.Barcode,
            Unit = Unit.Pcs,
            PurchaseQty = request.OpeningQuantity,
            MRP = request.Mrp,
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            TaxId = tax.Id,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId
        };

        db.Products.Add(product);
        db.Stocks.Add(stock);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/products/{product.Id}", new { product, stock });
    }

    private static string RequiredOrDefault(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
