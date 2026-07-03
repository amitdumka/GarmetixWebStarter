using Garmetix.Api.Auth;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
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
        group.MapGet("/paged", ListPagedAsync);
        group.MapGet("/options", OptionsAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);

        return group;
    }

    private static async Task<IReadOnlyList<ProductMasterRow>> ListAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var products = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Where(product =>
                !db.Stocks.Any(stock => stock.ProductId == product.Id) ||
                db.Stocks.Any(stock => stock.ProductId == product.Id && !stock.IsOFB))
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
            .Where(item => !item.IsOFB && productIds.Contains(item.ProductId))
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

    private static async Task<PagedProductMasterResponse> ListPagedAsync(
        HttpContext context,
        GarmetixDbContext db,
        int page = 1,
        int pageSize = 50,
        string? q = null,
        Guid? categoryId = null,
        Guid? subCategoryId = null,
        string? brand = null,
        Guid? vendorId = null,
        string? color = null,
        string? size = null,
        string stockMode = "in-stock",
        string? ageBucket = null,
        string? health = null,
        decimal? minStock = null,
        decimal? maxStock = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 25, 200);
        var term = q?.Trim().ToLowerInvariant();
        var brandTerm = brand?.Trim().ToLowerInvariant();
        var colorTerm = color?.Trim().ToLowerInvariant();
        var sizeTerm = NormalizeSize(size);
        var healthFilter = health?.Trim().ToLowerInvariant();
        var today = DateTime.Today;

        var scopedStocks = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(stock => !stock.IsOFB);
        var scopedDetails = WorkspaceScope.ApplyTo(db.ProductDetails.AsNoTracking(), context);

        var query = WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Include(item => item.ProductCategory)
            .Include(item => item.ProductSubCategory)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(product =>
                product.Name.ToLower().Contains(term) ||
                product.Barcode.ToLower().Contains(term) ||
                (product.HSNCode != null && product.HSNCode.ToLower().Contains(term)) ||
                (product.ProductCategory != null && product.ProductCategory.Name.ToLower().Contains(term)) ||
                (product.ProductSubCategory != null && product.ProductSubCategory.Name.ToLower().Contains(term)) ||
                scopedDetails.Any(detail =>
                    detail.ProductId == product.Id &&
                    ((detail.Brand != null && detail.Brand.ToLower().Contains(term)) ||
                     (detail.StyleCode != null && detail.StyleCode.ToLower().Contains(term)) ||
                     (detail.BaseColor != null && detail.BaseColor.ToLower().Contains(term)))));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(product => product.ProductCategoryId == categoryId.Value);
        }

        if (subCategoryId.HasValue)
        {
            query = query.Where(product => product.ProductSubCategoryId == subCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(brandTerm))
        {
            query = query.Where(product => scopedDetails.Any(detail => detail.ProductId == product.Id && detail.Brand != null && detail.Brand.ToLower().Contains(brandTerm)));
        }

        if (!string.IsNullOrWhiteSpace(colorTerm))
        {
            query = query.Where(product => scopedDetails.Any(detail => detail.ProductId == product.Id && detail.BaseColor != null && detail.BaseColor.ToLower().Contains(colorTerm)));
        }

        if (vendorId.HasValue)
        {
            query = query.Where(product => scopedDetails.Any(detail => detail.ProductId == product.Id && detail.VendorId == vendorId.Value));
        }

        if (stockMode.Equals("in-stock", StringComparison.OrdinalIgnoreCase) || stockMode.Equals("instock", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(product => scopedStocks.Any(stock => stock.ProductId == product.Id && (stock.PurchaseQty - stock.SoldQty) > 0));
        }
        else if (stockMode.Equals("out-of-stock", StringComparison.OrdinalIgnoreCase) || stockMode.Equals("outofstock", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(product => !scopedStocks.Any(stock => stock.ProductId == product.Id && (stock.PurchaseQty - stock.SoldQty) > 0));
        }

        if (minStock.HasValue)
        {
            query = query.Where(product => scopedStocks.Where(stock => stock.ProductId == product.Id).Select(stock => (decimal?)(stock.PurchaseQty - stock.SoldQty)).Sum() >= minStock.Value);
        }

        if (maxStock.HasValue)
        {
            query = query.Where(product => scopedStocks.Where(stock => stock.ProductId == product.Id).Select(stock => (decimal?)(stock.PurchaseQty - stock.SoldQty)).Sum() <= maxStock.Value);
        }

        if (!string.IsNullOrWhiteSpace(healthFilter) && !healthFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = ApplyProductHealthFilter(query, scopedStocks, scopedDetails, healthFilter, today);
        }

        if (!string.IsNullOrWhiteSpace(ageBucket) && !ageBucket.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = ApplyProductAgeFilter(query, db, context, ageBucket, today);
        }

        if (!string.IsNullOrWhiteSpace(sizeTerm) && !sizeTerm.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var sizeProductIds = await ResolveProductIdsBySizeAsync(query, scopedDetails, sizeTerm, cancellationToken);
            query = query.Where(product => sizeProductIds.Contains(product.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var products = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Barcode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(item => item.Id).ToArray();
        var stocks = await scopedStocks
            .Where(item => productIds.Contains(item.ProductId))
            .ToListAsync(cancellationToken);
        var details = await scopedDetails
            .Where(item => productIds.Contains(item.ProductId))
            .ToListAsync(cancellationToken);
        var lastInwardLookup = await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(movement => productIds.Contains(movement.ProductId) && movement.QuantityIn > 0)
            .GroupBy(movement => movement.ProductId)
            .Select(group => new { ProductId = group.Key, LastInwardAt = group.Max(movement => (DateTime?)movement.OnDate) })
            .ToDictionaryAsync(row => row.ProductId, row => row.LastInwardAt, cancellationToken);

        var rows = products.Select(product =>
        {
            var productStocks = stocks.Where(stock => stock.ProductId == product.Id).ToList();
            var row = ToRow(product, productStocks, details.FirstOrDefault(detail => detail.ProductId == product.Id));
            lastInwardLookup.TryGetValue(product.Id, out var lastInwardAt);
            row.LastInwardAt = lastInwardAt;
            row.AgeDays = lastInwardAt is null ? null : Math.Max(0, (today - lastInwardAt.Value.Date).Days);
            row.AgeBucket = StockReportCalculator.AgeBucket(row.CurrentStock, lastInwardAt, today);
            return row;
        }).ToList();

        var totalCurrentStock = await scopedStocks
            .Select(stock => (decimal?)(stock.PurchaseQty - stock.SoldQty))
            .SumAsync(cancellationToken) ?? 0;
        var inStockCount = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .CountAsync(product => scopedStocks.Any(stock => stock.ProductId == product.Id && (stock.PurchaseQty - stock.SoldQty) > 0), cancellationToken);
        var productCount = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context).CountAsync(cancellationToken);

        return new PagedProductMasterResponse(
            rows,
            total,
            page,
            pageSize,
            totalCurrentStock,
            await scopedStocks.Select(stock => (decimal?)((stock.PurchaseQty - stock.SoldQty) * stock.MRP)).SumAsync(cancellationToken) ?? 0,
            inStockCount,
            Math.Max(productCount - inStockCount, 0));
    }

    private static async Task<Guid[]> ResolveProductIdsBySizeAsync(
        IQueryable<Product> query,
        IQueryable<ProductDetail> scopedDetails,
        string sizeTerm,
        CancellationToken cancellationToken)
    {
        var productInfos = await query
            .Select(product => new { product.Id, product.Name, product.Descriptions })
            .Take(20000)
            .ToListAsync(cancellationToken);
        if (productInfos.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var productIds = productInfos.Select(item => item.Id).ToArray();
        var detailRows = await scopedDetails
            .Where(detail => productIds.Contains(detail.ProductId))
            .Select(detail => new { detail.ProductId, detail.StyleCode })
            .ToListAsync(cancellationToken);
        var detailLookup = detailRows
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.StyleCode).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)));

        return productInfos
            .Where(item =>
            {
                detailLookup.TryGetValue(item.Id, out var styleCode);
                var detected = NormalizeSize(DetectProductSize(item.Name, styleCode, item.Descriptions));
                return string.Equals(detected, sizeTerm, StringComparison.OrdinalIgnoreCase);
            })
            .Select(item => item.Id)
            .ToArray();
    }

    private static string? DetectProductSize(params string?[] sources)
    {
        foreach (var source in sources)
        {
            var size = DetectProductSizeFromText(source);
            if (!string.IsNullOrWhiteSpace(size))
            {
                return size;
            }
        }

        return null;
    }

    private static string? DetectProductSizeFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.ToUpperInvariant()
            .Replace("FREE-SIZE", "FREE SIZE")
            .Replace("FREESIZE", "FREE SIZE")
            .Replace("FREE_SIZE", "FREE SIZE");
        if (normalized.Contains("FREE SIZE"))
        {
            return "Free Size";
        }

        var tokens = Regex.Split(normalized, @"[^A-Z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();

        for (var index = tokens.Length - 1; index >= 0; index--)
        {
            var size = NormalizeSize(tokens[index]);
            if (!string.IsNullOrWhiteSpace(size) && IsLikelySizeToken(size))
            {
                return size;
            }
        }

        return null;
    }

    private static string? NormalizeSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var token = value.Trim().ToUpperInvariant()
            .Replace("FREE-SIZE", "FREE SIZE")
            .Replace("FREESIZE", "FREE SIZE")
            .Replace("FREE_SIZE", "FREE SIZE");
        if (token == "ALL") return "all";
        if (token is "FS" or "FREE") return "Free Size";
        if (token == "FREE SIZE") return "Free Size";
        if (token is "STD" or "STANDARD") return "STD";
        if (token is "NS" or "NA" or "N/A") return "NS";
        if (token.StartsWith("C") && token.Length > 1 && int.TryParse(token[1..], out var cSize)) return cSize.ToString();
        if (token.StartsWith("T") && token.Length > 1 && int.TryParse(token[1..], out var tSize)) return tSize.ToString();
        if (token.StartsWith("B") && token.Length > 1 && int.TryParse(token[1..], out var bSize)) return bSize.ToString();
        if (int.TryParse(token, out var number) && number is >= 2 and <= 60) return number.ToString();
        return token switch
        {
            "XS" or "S" or "M" or "L" or "XL" or "XXL" or "XXXL" or "XXXXL" => token,
            _ => null
        };
    }

    private static bool IsLikelySizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token is "XS" or "S" or "M" or "L" or "XL" or "XXL" or "XXXL" or "XXXXL" or "STD" or "NS" or "Free Size")
        {
            return true;
        }

        return int.TryParse(token, out var size) && size is >= 2 and <= 60;
    }

    private static int SizeSortKey(string value)
    {
        var normalized = NormalizeSize(value) ?? value;
        if (int.TryParse(normalized, out var number))
        {
            return 100 + number;
        }

        return normalized switch
        {
            "XS" => 10,
            "S" => 20,
            "M" => 30,
            "L" => 40,
            "XL" => 50,
            "XXL" => 60,
            "XXXL" => 70,
            "XXXXL" => 80,
            "STD" => 90,
            "Free Size" => 95,
            "NS" => 99,
            _ => 1000
        };
    }

    private static IQueryable<Product> ApplyProductHealthFilter(
        IQueryable<Product> query,
        IQueryable<Stock> scopedStocks,
        IQueryable<ProductDetail> scopedDetails,
        string healthFilter,
        DateTime today)
    {
        return healthFilter switch
        {
            "low-stock" => query.Where(product => scopedStocks.Where(stock => stock.ProductId == product.Id).Select(stock => (decimal?)(stock.PurchaseQty - stock.SoldQty)).Sum() > 0
                && scopedStocks.Where(stock => stock.ProductId == product.Id).Select(stock => (decimal?)(stock.PurchaseQty - stock.SoldQty)).Sum() <= 2),
            "dead-stock" => query.Where(product => scopedStocks.Any(stock => stock.ProductId == product.Id && (stock.PurchaseQty - stock.SoldQty) > 0)
                && scopedStocks.Where(stock => stock.ProductId == product.Id).Max(stock => (DateTime?)stock.UpdatedAt) < today.AddDays(-180)),
            "high-value" => query.Where(product => scopedStocks.Where(stock => stock.ProductId == product.Id).Select(stock => (decimal?)((stock.PurchaseQty - stock.SoldQty) * stock.MRP)).Sum() >= 10000),
            "missing-hsn" => query.Where(product => product.HSNCode == null || product.HSNCode == ""),
            "missing-category" => query.Where(product => product.ProductCategoryId == Guid.Empty || product.ProductSubCategoryId == Guid.Empty),
            "missing-brand" => query.Where(product => !scopedDetails.Any(detail => detail.ProductId == product.Id && detail.Brand != null && detail.Brand != "")),
            "missing-vendor" => query.Where(product => !scopedDetails.Any(detail => detail.ProductId == product.Id && detail.VendorId != null)),
            "missing-color" => query.Where(product => !scopedDetails.Any(detail => detail.ProductId == product.Id && detail.BaseColor != null && detail.BaseColor != "")),
            _ => query
        };
    }

    private static IQueryable<Product> ApplyProductAgeFilter(
        IQueryable<Product> query,
        GarmetixDbContext db,
        HttpContext context,
        string ageBucket,
        DateTime today)
    {
        var movements = WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(movement => movement.QuantityIn > 0);

        return ageBucket switch
        {
            "0-30 Days" => query.Where(product => movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) >= today.AddDays(-30)),
            "31-60 Days" => query.Where(product => movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) < today.AddDays(-30) && movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) >= today.AddDays(-60)),
            "61-90 Days" => query.Where(product => movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) < today.AddDays(-60) && movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) >= today.AddDays(-90)),
            "91-180 Days" => query.Where(product => movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) < today.AddDays(-90) && movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) >= today.AddDays(-180)),
            "180+ Days" => query.Where(product => movements.Where(movement => movement.ProductId == product.Id).Max(movement => (DateTime?)movement.OnDate) < today.AddDays(-180)),
            "No Receipt History" => query.Where(product => !movements.Any(movement => movement.ProductId == product.Id)),
            "Out of Stock" => query.Where(product => !WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context).Any(stock => !stock.IsOFB && stock.ProductId == product.Id && (stock.PurchaseQty - stock.SoldQty) > 0)),
            _ => query
        };
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

        var brands = await WorkspaceScope.ApplyTo(db.ProductDetails.AsNoTracking(), context)
            .Where(item => item.Brand != null && item.Brand != "")
            .Select(item => item.Brand!)
            .Distinct()
            .OrderBy(item => item)
            .Take(500)
            .ToListAsync(cancellationToken);

        var baseColors = await WorkspaceScope.ApplyTo(db.ProductDetails.AsNoTracking(), context)
            .Where(item => item.BaseColor != null && item.BaseColor != "")
            .Select(item => item.BaseColor!)
            .Distinct()
            .OrderBy(item => item)
            .Take(500)
            .ToListAsync(cancellationToken);

        var sizeSources = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Select(item => new { item.Name, item.Descriptions })
            .Take(5000)
            .ToListAsync(cancellationToken);
        var sizeDetailSources = await WorkspaceScope.ApplyTo(db.ProductDetails.AsNoTracking(), context)
            .Select(item => new { item.StyleCode })
            .Take(5000)
            .ToListAsync(cancellationToken);
        var sizes = sizeSources
            .Select(item => DetectProductSize(item.Name, item.Descriptions, null))
            .Concat(sizeDetailSources.Select(item => DetectProductSize(item.StyleCode, null, null)))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(SizeSortKey)
            .ThenBy(item => item)
            .Take(250)
            .ToList();

        return new ProductMasterOptionsResponse(
            categories,
            subCategories,
            taxes,
            vendors,
            brands,
            baseColors,
            sizes,
            EnumOptions<Unit>(),
            EnumOptions<TaxType>(),
            EnumOptions<ProductType>("Readmade"),
            EnumOptions<ProductGroup>(),
            EnumOptions<StockType>());
    }

    private static async Task<IResult> CreateAsync(
        ProductMasterRequest request,
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var validation = ValidateBasics(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => CreateInTransactionAsync(request, context, db, stockLedger, cancellationToken));
    }

    private static async Task<IResult> CreateInTransactionAsync(
        ProductMasterRequest request,
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var scope = await ResolveScopeAsync(request, context, db, cancellationToken);
        if (scope is null)
        {
            return Results.BadRequest(new { message = "Select a valid company, store group, and store before adding a product." });
        }

        var barcode = request.Barcode.Trim();
        await DocumentNumberGenerator.LockStockKeyAsync(db, scope.Value.CompanyId, scope.Value.StoreGroupId, scope.Value.StoreId, Guid.Empty, barcode, cancellationToken);

        if (await db.Stocks.AnyAsync(item =>
                item.CompanyId == scope.Value.CompanyId &&
                item.Barcode == barcode &&
                !item.IsOFB,
                cancellationToken))
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
            PurchaseQty = 0,
            CostPrice = 0,
            MRP = product.MRP,
            TaxRate = product.TaxRate,
            TaxType = product.TaxType,
            TaxId = tax.Id,
            StockType = request.StockType ?? StockType.Billed,
            IsOFB = false,
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

        if (request.OpeningQuantity > 0)
        {
            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "Opening",
                QuantityIn = request.OpeningQuantity,
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
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Results.Created($"/api/inventory/product-master/{product.Id}", ToRow(product, new[] { stock }, detail));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ProductMasterRequest request,
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var validation = ValidateBasics(request);
        if (validation is not null)
        {
            return Results.BadRequest(new { message = validation });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(() => UpdateInTransactionAsync(id, request, context, db, stockLedger, cancellationToken));
    }

    private static async Task<IResult> UpdateInTransactionAsync(
        Guid id,
        ProductMasterRequest request,
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var product = await WorkspaceScope.ApplyTo(db.Products, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return Results.NotFound();
        }

        var barcode = request.Barcode.Trim();
        var stock = await ResolveStockForUpdateAsync(product.Id, request.StoreId, context, db, cancellationToken);
        (Guid CompanyId, Guid StoreGroupId, Guid StoreId)? scope = stock is not null
            ? (stock.CompanyId, stock.StoreGroupId, stock.StoreId)
            : await ResolveScopeAsync(request, context, db, cancellationToken);
        if (scope is null)
        {
            return Results.BadRequest(new { message = "Select a valid store before updating stock details." });
        }

        await DocumentNumberGenerator.LockStockKeyAsync(db, scope.Value.CompanyId, scope.Value.StoreGroupId, scope.Value.StoreId, product.Id, barcode, cancellationToken);

        if (await db.Stocks.AnyAsync(item =>
                item.CompanyId == product.CompanyId &&
                item.ProductId != product.Id &&
                item.Barcode == barcode &&
                !item.IsOFB,
                cancellationToken))
        {
            return Results.Conflict(new { message = $"Barcode {barcode} already exists in this company." });
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

        var isNewStock = stock is null;
        stock ??= new Stock
        {
            ProductId = product.Id,
            Barcode = barcode,
            PurchaseQty = 0,
            CostPrice = 0,
            CompanyId = scope.Value.CompanyId,
            StoreGroupId = scope.Value.StoreGroupId,
            StoreId = scope.Value.StoreId
        };

        stock.Barcode = barcode;
        stock.HSNCode = product.HSNCode;
        stock.Unit = product.Unit;
        stock.MRP = product.MRP;
        stock.TaxRate = product.TaxRate;
        stock.TaxType = product.TaxType;
        stock.TaxId = tax.Id;
        stock.StockType = request.StockType ?? stock.StockType;
        stock.IsOFB = false;

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

        if (isNewStock && request.OpeningQuantity > 0)
        {
            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "Opening",
                QuantityIn = request.OpeningQuantity,
                CostPrice = request.CostPrice,
                MRP = product.MRP,
                TaxRate = product.TaxRate,
                HSNCode = product.HSNCode,
                SourceType = "ProductMaster",
                SourceId = product.Id,
                SourceNumber = barcode,
                Remarks = "Opening quantity created while updating product master",
                CompanyId = scope.Value.CompanyId,
                StoreGroupId = scope.Value.StoreGroupId,
                StoreId = scope.Value.StoreId
            }, cancellationToken);
        }
        else if (!isNewStock)
        {
            await stockLedger.RebuildProjectionAsync(stock, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
            SizeLabel = DetectProductSize(product.Name, detail?.StyleCode, product.Descriptions),
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
        var query = WorkspaceScope.ApplyTo(db.Stocks, context)
            .Where(item => item.ProductId == productId && !item.IsOFB);
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
