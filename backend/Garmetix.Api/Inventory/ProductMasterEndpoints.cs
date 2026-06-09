using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using InventoryProductSubCategory = Garmetix.Core.Models.Inventory.ProductSubCategory;

namespace Garmetix.Api.Inventory;

public static class ProductMasterEndpoints
{
    public static RouteGroupBuilder MapInventoryProductMasterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/product-master")
            .WithTags("Inventory Product Master")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("/", ListAsync);
        group.MapGet("/options", OptionsAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);

        return group;
    }

    private static async Task<IReadOnlyList<ProductMasterRow>> ListAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var products = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Include(item => item.ProductCategory)
            .Include(item => item.ProductSubCategory)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(item => item.Id).ToArray();
        if (productIds.Length == 0)
        {
            return Array.Empty<ProductMasterRow>();
        }

        var stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => productIds.Contains(item.ProductId))
            .ToListAsync(cancellationToken);

        var details = await WorkspaceScope.ApplyTo(db.ProductDetails.AsNoTracking(), context)
            .Where(item => productIds.Contains(item.ProductId))
            .ToListAsync(cancellationToken);

        return products.Select(product => ToRow(
                product,
                stocks.Where(stock => stock.ProductId == product.Id).ToList(),
                details.FirstOrDefault(detail => detail.ProductId == product.Id)))
            .ToList();
    }

    private static async Task<ProductMasterOptionsResponse> OptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var categories = await WorkspaceScope.ApplyTo(db.ProductCategories.AsNoTracking(), context)
            .OrderBy(item => item.ProductGroup)
            .ThenBy(item => item.Name)
            .Select(item => new ProductCategoryOptionDto(item.Id, item.Name, item.ProductGroup, item.IsActive))
            .ToListAsync(cancellationToken);

        var subCategories = await WorkspaceScope.ApplyTo(db.ProductSubCategories.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new ProductSubCategoryOptionDto(item.Id, item.Name, item.CategoryId))
            .ToListAsync(cancellationToken);

        var taxes = await WorkspaceScope.ApplyTo(db.Taxes.AsNoTracking(), context)
            .OrderBy(item => item.CompositeRate)
            .Select(item => new TaxOptionDto(item.Id, item.Name, item.CompositeRate, item.TaxType))
            .ToListAsync(cancellationToken);

        var vendors = await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new VendorOptionDto(item.Id, item.Name, item.MobileNumber, item.GSTIN))
            .ToListAsync(cancellationToken);

        return new ProductMasterOptionsResponse(
            categories,
            subCategories,
            taxes,
            vendors,
            EnumOptions<Unit>(),
            EnumOptions<TaxType>(),
            EnumOptions<ProductType>("Readmade"),
            EnumOptions<ProductGroup>(),
            EnumOptions<StockType>());
    }

    private static async Task<IResult> CreateAsync(ProductMasterRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var validation = ValidateBasics(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var scope = await ResolveScopeAsync(request, context, db, cancellationToken);
        if (scope is null)
        {
            return Results.BadRequest(new { message = "Select a valid company, store group, and store before adding a product." });
        }

        var barcode = request.Barcode.Trim();
        if (await db.Products.AnyAsync(item => item.CompanyId == scope.Value.CompanyId && item.Barcode == barcode, cancellationToken))
        {
            return Results.Conflict(new { message = $"Barcode {barcode} already exists in this company." });
        }

        var tax = await ResolveTaxAsync(request, db, cancellationToken);
        var category = await ResolveCategoryAsync(request.ProductCategoryId, scope.Value.CompanyId, request.ProductGroup ?? ProductGroup.Shirting, db, cancellationToken);
        var subCategory = await ResolveSubCategoryAsync(request.ProductSubCategoryId, category.Id, scope.Value.CompanyId, db, cancellationToken);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Barcode = barcode,
            Descriptions = NullIfWhiteSpace(request.Descriptions),
            HSNCode = NullIfWhiteSpace(request.HSNCode),
            MRP = request.Mrp,
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            Unit = request.Unit ?? Unit.Pcs,
            ProductType = request.ProductType ?? ProductType.Fabric,
            ProductGroup = request.ProductGroup ?? category.ProductGroup ?? ProductGroup.Shirting,
            ProductCategoryId = category.Id,
            ProductSubCategoryId = subCategory.Id,
            CompanyId = scope.Value.CompanyId,
            StoreGroupId = scope.Value.StoreGroupId
        };

        var stock = new Stock
        {
            ProductId = product.Id,
            Barcode = barcode,
            HSNCode = product.HSNCode,
            Unit = product.Unit,
            PurchaseQty = request.OpeningQuantity,
            CostPrice = request.CostPrice,
            MRP = product.MRP,
            TaxRate = product.TaxRate,
            TaxType = product.TaxType,
            TaxId = tax.Id,
            StockType = request.StockType ?? StockType.Billed,
            CompanyId = scope.Value.CompanyId,
            StoreGroupId = scope.Value.StoreGroupId,
            StoreId = scope.Value.StoreId
        };

        var detail = BuildDetail(request, product, scope.Value.CompanyId);

        if (!WorkspaceScope.CanWrite(product, context, out var productMessage))
        {
            return Results.BadRequest(new { message = productMessage ?? "Selected product company/group is outside your access scope." });
        }

        if (!WorkspaceScope.CanWrite(stock, context, out var stockMessage))
        {
            return Results.BadRequest(new { message = stockMessage ?? "Selected product stock store is outside your access scope." });
        }

        if (detail is not null && !WorkspaceScope.CanWrite(detail, context, out var detailMessage))
        {
            return Results.BadRequest(new { message = detailMessage ?? "Selected product detail company is outside your access scope." });
        }

        db.Products.Add(product);
        db.Stocks.Add(stock);
        if (detail is not null)
        {
            db.ProductDetails.Add(detail);
        }

        if (request.OpeningQuantity != 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                StockId = stock.Id,
                ProductId = product.Id,
                Barcode = barcode,
                MovementType = "Opening",
                QuantityIn = request.OpeningQuantity > 0 ? request.OpeningQuantity : 0,
                QuantityOut = request.OpeningQuantity < 0 ? Math.Abs(request.OpeningQuantity) : 0,
                CostPrice = request.CostPrice,
                MRP = product.MRP,
                TaxRate = product.TaxRate,
                HSNCode = product.HSNCode,
                SourceType = "ProductMaster",
                SourceId = product.Id,
                SourceNumber = barcode,
                Remarks = "Opening quantity from Stage 3A Product Master UI",
                CompanyId = scope.Value.CompanyId,
                StoreGroupId = scope.Value.StoreGroupId,
                StoreId = scope.Value.StoreId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/inventory/product-master/{product.Id}", ToRow(product, new[] { stock }, detail));
    }

    private static async Task<IResult> UpdateAsync(Guid id, ProductMasterRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var validation = ValidateBasics(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var product = await WorkspaceScope.ApplyTo(db.Products, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        var barcode = request.Barcode.Trim();
        if (await db.Products.AnyAsync(item => item.CompanyId == product.CompanyId && item.Id != product.Id && item.Barcode == barcode, cancellationToken))
        {
            return Results.Conflict(new { message = $"Barcode {barcode} already exists in this company." });
        }

        var stock = await ResolveStockForUpdateAsync(product.Id, request.StoreId, context, db, cancellationToken);
        (Guid CompanyId, Guid StoreGroupId, Guid StoreId)? scope = stock is not null
            ? (stock.CompanyId, stock.StoreGroupId, stock.StoreId)
            : await ResolveScopeAsync(request, context, db, cancellationToken);
        if (scope is null)
        {
            return Results.BadRequest(new { message = "Select a valid store before updating stock details." });
        }

        var tax = await ResolveTaxAsync(request, db, cancellationToken);
        var category = await ResolveCategoryAsync(request.ProductCategoryId, product.CompanyId, request.ProductGroup ?? product.ProductGroup, db, cancellationToken);
        var subCategory = await ResolveSubCategoryAsync(request.ProductSubCategoryId, category.Id, product.CompanyId, db, cancellationToken);

        product.Name = request.Name.Trim();
        product.Barcode = barcode;
        product.Descriptions = NullIfWhiteSpace(request.Descriptions);
        product.HSNCode = NullIfWhiteSpace(request.HSNCode);
        product.MRP = request.Mrp;
        product.TaxRate = tax.CompositeRate;
        product.TaxType = tax.TaxType;
        product.Unit = request.Unit ?? product.Unit;
        product.ProductType = request.ProductType ?? product.ProductType;
        product.ProductGroup = request.ProductGroup ?? category.ProductGroup ?? product.ProductGroup;
        product.ProductCategoryId = category.Id;
        product.ProductSubCategoryId = subCategory.Id;

        stock ??= new Stock
        {
            ProductId = product.Id,
            PurchaseQty = request.OpeningQuantity,
            CompanyId = scope.Value.CompanyId,
            StoreGroupId = scope.Value.StoreGroupId,
            StoreId = scope.Value.StoreId,
            Barcode=barcode
        };

        stock.Barcode = barcode;
        stock.HSNCode = product.HSNCode;
        stock.Unit = product.Unit;
        stock.CostPrice = request.CostPrice;
        stock.MRP = product.MRP;
        stock.TaxRate = product.TaxRate;
        stock.TaxType = product.TaxType;
        stock.TaxId = tax.Id;
        stock.StockType = request.StockType ?? stock.StockType;

        var detail = await db.ProductDetails.FirstOrDefaultAsync(item => item.CompanyId == product.CompanyId && item.ProductId == product.Id, cancellationToken);
        if (ShouldKeepDetail(request))
        {
            if (detail is null)
            {
                detail = BuildDetail(request, product, product.CompanyId);
                if (detail is not null)
                {
                    db.ProductDetails.Add(detail);
                }
            }
            else
            {
                detail.Barcode = barcode;
                detail.StyleCode = NullIfWhiteSpace(request.StyleCode);
                detail.BaseColor = NullIfWhiteSpace(request.BaseColor);
                detail.Brand = NullIfWhiteSpace(request.Brand);
                detail.VendorId = request.VendorId;
            }
        }
        else if (detail is not null)
        {
            detail.StyleCode = null;
            detail.BaseColor = null;
            detail.Brand = null;
            detail.VendorId = null;
        }

        if (!WorkspaceScope.CanWrite(product, context, out var productMessage))
        {
            return Results.BadRequest(new { message = productMessage ?? "Selected product company/group is outside your access scope." });
        }

        if (!WorkspaceScope.CanWrite(stock, context, out var stockMessage))
        {
            return Results.BadRequest(new { message = stockMessage ?? "Selected product stock store is outside your access scope." });
        }

        if (detail is not null && !WorkspaceScope.CanWrite(detail, context, out var detailMessage))
        {
            return Results.BadRequest(new { message = detailMessage ?? "Selected product detail company is outside your access scope." });
        }

        if (stock.Id == Guid.Empty || db.Entry(stock).State == EntityState.Detached)
        {
            db.Stocks.Add(stock);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToRow(product, new[] { stock }, detail));
    }

    private static ProductMasterRow ToRow(Product product, IReadOnlyList<Stock> stocks, ProductDetail? detail)
    {
        var selectedStock = stocks.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).FirstOrDefault();
        var purchaseQty = stocks.Sum(item => item.PurchaseQty);
        var soldQty = stocks.Sum(item => item.SoldQty);

        return new ProductMasterRow
        {
            Id = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            Descriptions = product.Descriptions,
            HSNCode = product.HSNCode,
            Mrp = product.MRP,
            TaxRate = product.TaxRate,
            TaxType = product.TaxType,
            Unit = product.Unit,
            ProductType = product.ProductType,
            ProductGroup = product.ProductGroup,
            ProductCategoryId = product.ProductCategoryId,
            ProductSubCategoryId = product.ProductSubCategoryId,
            CategoryName = product.ProductCategory?.Name,
            SubCategoryName = product.ProductSubCategory?.Name,
            StockId = selectedStock?.Id,
            PurchaseQty = purchaseQty,
            SoldQty = soldQty,
            CurrentStock = purchaseQty - soldQty,
            CostPrice = selectedStock?.CostPrice ?? 0,
            StockType = selectedStock?.StockType ?? StockType.Billed,
            TaxId = selectedStock?.TaxId,
            CompanyId = product.CompanyId,
            StoreGroupId = product.StoreGroupId,
            StoreId = selectedStock?.StoreId,
            StyleCode = detail?.StyleCode,
            BaseColor = detail?.BaseColor,
            Brand = detail?.Brand,
            VendorId = detail?.VendorId
        };
    }

    private static async Task<(Guid CompanyId, Guid StoreGroupId, Guid StoreId)?> ResolveScopeAsync(
        ProductMasterRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var companyId = NormalizeGuid(request.CompanyId) ?? WorkspaceScope.ClaimGuid(context, "companyId");
        var storeGroupId = NormalizeGuid(request.StoreGroupId) ?? WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var storeId = NormalizeGuid(request.StoreId) ?? WorkspaceScope.ClaimGuid(context, "storeId");

        if (storeId.HasValue)
        {
            var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .FirstOrDefaultAsync(item => item.Id == storeId.Value, cancellationToken);
            if (store is null)
            {
                return null;
            }

            return (store.CompanyId, store.StoreGroupId, store.Id);
        }

        var fallbackStore = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(item => !companyId.HasValue || item.CompanyId == companyId.Value)
            .Where(item => !storeGroupId.HasValue || item.StoreGroupId == storeGroupId.Value)
            .OrderBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return fallbackStore is null ? null : (fallbackStore.CompanyId, fallbackStore.StoreGroupId, fallbackStore.Id);
    }

    private static async Task<Stock?> ResolveStockForUpdateAsync(Guid productId, Guid? requestStoreId, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var storeId = NormalizeGuid(requestStoreId) ?? WorkspaceScope.ClaimGuid(context, "storeId");
        var query = WorkspaceScope.ApplyTo(db.Stocks, context).Where(item => item.ProductId == productId);
        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return await query.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<Tax> ResolveTaxAsync(ProductMasterRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var taxId = NormalizeGuid(request.TaxId);
        var tax = taxId.HasValue
            ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == taxId.Value, cancellationToken)
            : null;

        tax ??= await db.Taxes.FirstOrDefaultAsync(
            item => item.CompositeRate == (request.TaxRate ?? 0) && item.TaxType == (request.TaxType ?? TaxType.GST),
            cancellationToken);

        if (tax is not null)
        {
            return tax;
        }

        tax = new Tax
        {
            Name = $"{request.TaxType ?? TaxType.GST} {request.TaxRate ?? 0:0.##}",
            CompositeRate = request.TaxRate ?? 0,
            TaxType = request.TaxType ?? TaxType.GST
        };
        db.Taxes.Add(tax);
        return tax;
    }

    private static async Task<InventoryProductCategory> ResolveCategoryAsync(Guid? categoryId, Guid companyId, ProductGroup group, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeGuid(categoryId);
        if (normalizedId.HasValue)
        {
            var selected = await db.ProductCategories.FirstOrDefaultAsync(item => item.Id == normalizedId.Value && item.CompanyId == companyId, cancellationToken);
            if (selected is not null)
            {
                return selected;
            }
        }

        var category = await db.ProductCategories.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.ProductGroup == group && item.Name == "General",
            cancellationToken);

        if (category is not null)
        {
            return category;
        }

        category = new InventoryProductCategory
        {
            CompanyId = companyId,
            Name = "General",
            ProductGroup = group,
            IsActive = true
        };
        db.ProductCategories.Add(category);
        return category;
    }

    private static async Task<InventoryProductSubCategory> ResolveSubCategoryAsync(Guid? subCategoryId, Guid categoryId, Guid companyId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var normalizedId = NormalizeGuid(subCategoryId);
        if (normalizedId.HasValue)
        {
            var selected = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.Id == normalizedId.Value && item.CompanyId == companyId, cancellationToken);
            if (selected is not null)
            {
                return selected;
            }
        }

        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(
            item => item.CompanyId == companyId && item.CategoryId == categoryId && item.Name == "General",
            cancellationToken);

        if (subCategory is not null)
        {
            return subCategory;
        }

        subCategory = new InventoryProductSubCategory
        {
            CompanyId = companyId,
            CategoryId = categoryId,
            Name = "General"
        };
        db.ProductSubCategories.Add(subCategory);
        return subCategory;
    }

    private static ProductDetail? BuildDetail(ProductMasterRequest request, Product product, Guid companyId)
    {
        if (!ShouldKeepDetail(request))
        {
            return null;
        }

        return new ProductDetail
        {
            ProductId = product.Id,
            Barcode = product.Barcode,
            StyleCode = NullIfWhiteSpace(request.StyleCode),
            BaseColor = NullIfWhiteSpace(request.BaseColor),
            Brand = NullIfWhiteSpace(request.Brand),
            VendorId = request.VendorId,
            CompanyId = companyId
        };
    }

    private static string? ValidateBasics(ProductMasterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Product name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return "Barcode is required.";
        }

        if (request.Mrp < 0)
        {
            return "MRP cannot be negative.";
        }

        if (request.OpeningQuantity < 0)
        {
            return "Opening quantity cannot be negative from Product Master. Use stock adjustment for reductions.";
        }

        return null;
    }

    private static bool ShouldKeepDetail(ProductMasterRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.StyleCode)
            || !string.IsNullOrWhiteSpace(request.BaseColor)
            || !string.IsNullOrWhiteSpace(request.Brand)
            || NormalizeGuid(request.VendorId).HasValue;
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Guid? NormalizeGuid(Guid? value) => value.HasValue && value.Value != Guid.Empty ? value.Value : null;

    private static IReadOnlyList<EnumOptionDto> EnumOptions<TEnum>(params string[] excludedNames) where TEnum : struct, Enum
    {
        var excluded = excludedNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Enum.GetNames<TEnum>()
            .Where(name => !excluded.Contains(name))
            .Select(name => new EnumOptionDto(Convert.ToInt32(Enum.Parse<TEnum>(name)), name))
            .GroupBy(item => item.Value)
            .Select(group => group.First())
            .OrderBy(item => item.Value)
            .ToList();
    }
}
