using Garmetix.Api.Auth;
using Garmetix.Api.Numbering;
using Garmetix.Api.ProductLookup;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Core.Models.Stores;
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Garmetix.Api.NonGstGoods;

public static class NonGstGoodsEndpoints
{
    public static RouteGroupBuilder MapNonGstGoodsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/non-gst-goods")
            .WithTags("Non-GST Goods")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("/options", OptionsAsync);
        group.MapGet("/report", ReportAsync);
        group.MapGet("/documents/{id:guid}", GetDocumentAsync);
        group.MapGet("/documents/{id:guid}/print", PrintAsync);
        group.MapGet("/documents/{id:guid}/pdf", DownloadPdfAsync);
        group.MapPost("/purchase", PurchaseAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/sale", SaleAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPut("/documents/{id:guid}", UpdateAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/documents/{id:guid}", DeleteAsync).RequireAuthorization(GarmetixPolicies.Delete);
        return group;
    }

    private static async Task<NonGstGoodsOptionsDto> OptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var stores = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);
        var storeNames = stores.ToDictionary(item => item.Id, item => item.Name);

        var stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => item.IsOFB && item.PurchaseQty - item.SoldQty > 0)
            .Include(item => item.Product)
            .OrderBy(item => item.Product != null ? item.Product.Name : item.Barcode)
            .ThenBy(item => item.Barcode)
            .Select(item => new NonGstStockOptionDto(
                item.ProductId,
                item.Id,
                item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                item.PurchaseQty - item.SoldQty,
                item.CostPrice,
                item.MRP,
                item.StoreId,
                string.Empty,
                string.Empty))
            .ToListAsync(cancellationToken);

        stocks = stocks.Select(item => item with
        {
            StoreName = storeNames.GetValueOrDefault(item.StoreId, "Store"),
            Label = $"{item.ProductName} | {item.Barcode} | Qty {item.CurrentStock:0.##} | MRP {item.MRP:0.##}"
        }).ToList();

        var vendors = await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new NonGstPartyOptionDto(item.Id, item.Name, item.MobileNumber, item.GSTIN))
            .Take(200)
            .ToListAsync(cancellationToken);

        var customers = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new NonGstPartyOptionDto(item.Id, item.Name, item.MobileNumber, item.GSTIN))
            .Take(200)
            .ToListAsync(cancellationToken);

        var recent = await BuildDocumentQuery(context, db, DateTime.Today.AddDays(-30), DateTime.Today.AddDays(1), 25)
            .ToListAsync(cancellationToken);

        return new NonGstGoodsOptionsDto(stocks, vendors, customers, recent);
    }

    private static async Task<IResult> PurchaseAsync(
        NonGstGoodsRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        CancellationToken cancellationToken)
        => await PostAsync(NonGstGoodsDocumentType.Purchase, request, context, db, documentNumbers, cancellationToken);

    private static async Task<IResult> SaleAsync(
        NonGstGoodsRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        CancellationToken cancellationToken)
        => await PostAsync(NonGstGoodsDocumentType.Sale, request, context, db, documentNumbers, cancellationToken);

    private static async Task<IResult> UpdateAsync(
        Guid id,
        NonGstGoodsRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        CancellationToken cancellationToken)
    {
        var existingType = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
            .Where(item => item.Id == id)
            .Select(item => (NonGstGoodsDocumentType?)item.DocumentType)
            .FirstOrDefaultAsync(cancellationToken);
        return existingType.HasValue
            ? await PostAsync(existingType.Value, request, context, db, documentNumbers, cancellationToken, id)
            : Results.NotFound(new { message = "Off Book document was not found." });
    }

    private static async Task<IResult> PostAsync(
        NonGstGoodsDocumentType documentType,
        NonGstGoodsRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        CancellationToken cancellationToken,
        Guid? documentId = null)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "Add at least one item." });
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var storeResult = await ResolveDocumentStoreAsync(documentType, request, context, db, cancellationToken);
                if (storeResult.Store is null)
                {
                    return Results.BadRequest(new { message = storeResult.ErrorMessage ?? "No store is available for this operation." });
                }
                var store = storeResult.Store;
                var onDate = request.OnDate?.Date ?? DateTime.Today;

                var partyName = await ResolvePartyNameAsync(documentType, request, db, cancellationToken);
                NonGstGoodsDocument document;
                if (documentId.HasValue)
                {
                    document = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments, context)
                        .FirstOrDefaultAsync(item => item.Id == documentId.Value, cancellationToken)
                        ?? throw new InvalidOperationException("Off Book document was not found.");
                    await ReverseDocumentStockAsync(document, db, cancellationToken);
                }
                else
                {
                    var documentNumber = documentType == NonGstGoodsDocumentType.Purchase
                        ? await documentNumbers.NextNonGstPurchaseAsync(store.CompanyId, store.StoreGroupId, store.Id, onDate, cancellationToken)
                        : await documentNumbers.NextNonGstSaleAsync(store.CompanyId, store.StoreGroupId, store.Id, onDate, cancellationToken);
                    document = new NonGstGoodsDocument
                    {
                        Id = Guid.NewGuid(),
                        DocumentNumber = documentNumber
                    };
                    db.NonGstGoodsDocuments.Add(document);
                }

                document.OnDate = onDate;
                document.DocumentType = documentType;
                document.PartyName = partyName;
                document.VendorId = documentType == NonGstGoodsDocumentType.Purchase ? request.VendorId : null;
                document.CustomerId = documentType == NonGstGoodsDocumentType.Sale ? request.CustomerId : null;
                document.PaymentMode = request.PaymentMode;
                document.ReferenceNumber = Clean(request.ReferenceNumber);
                document.Remarks = Clean(request.Remarks);
                document.LedgerId = null;
                document.CompanyId = store.CompanyId;
                document.StoreGroupId = store.StoreGroupId;
                document.StoreId = store.Id;

                decimal gross = 0;
                decimal discount = 0;

                foreach (var line in request.Items)
                {
                    if (line.Quantity <= 0)
                    {
                        return Results.BadRequest(new { message = "Item quantity must be greater than zero." });
                    }
                    if (line.Rate < 0 || line.DiscountAmount < 0)
                    {
                        return Results.BadRequest(new { message = "Item rate/discount cannot be negative." });
                    }

                    var lineStock = await ResolveOrCreateNonGstStockAsync(documentType, line, store, context, db, cancellationToken);
                    await DocumentNumberGenerator.LockStockKeyAsync(db, lineStock.CompanyId, lineStock.StoreGroupId, lineStock.StoreId, lineStock.ProductId, lineStock.Barcode, cancellationToken);

                    if (!lineStock.IsOFB)
                    {
                        return Results.BadRequest(new { message = $"Stock {lineStock.Barcode} is not marked Non-GST / out-of-scope." });
                    }

                    if (documentType == NonGstGoodsDocumentType.Sale && lineStock.CurrentStock < line.Quantity)
                    {
                        return Results.BadRequest(new { message = $"Cannot sell {line.Quantity:0.##}. Available Non-GST stock for {lineStock.Barcode} is {lineStock.CurrentStock:0.##}." });
                    }

                    var lineGross = Money(line.Quantity * line.Rate);
                    var lineDiscount = Math.Min(Money(line.DiscountAmount), lineGross);
                    var taxableAmount = lineGross - lineDiscount;
                    const decimal taxRate = 0m;
                    const decimal taxAmount = 0m;
                    var amount = taxableAmount + taxAmount;
                    var costRate = documentType == NonGstGoodsDocumentType.Purchase
                        ? Money(line.CostPrice ?? line.Rate)
                        : Money(lineStock.CostPrice);
                    var costAmount = Money(costRate * line.Quantity);

                    gross += lineGross;
                    discount += lineDiscount;

                    if (documentType == NonGstGoodsDocumentType.Purchase)
                    {
                        lineStock.PurchaseQty += line.Quantity;
                        lineStock.CostPrice = costRate;
                        lineStock.MRP = Money(line.MRP ?? Math.Max(lineStock.MRP, line.Rate));
                    }
                    else
                    {
                        lineStock.SoldQty += line.Quantity;
                        lineStock.SoldValue += amount;
                    }

                    db.NonGstGoodsItems.Add(new NonGstGoodsItem
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = document.Id,
                        ProductId = lineStock.ProductId,
                        StockId = lineStock.Id,
                        Barcode = lineStock.Barcode,
                        ProductName = lineStock.Product?.Name ?? Clean(line.ProductName) ?? lineStock.Barcode,
                        Quantity = line.Quantity,
                        Rate = line.Rate,
                        GrossAmount = lineGross,
                        DiscountAmount = lineDiscount,
                        TaxableAmount = taxableAmount,
                        TaxRate = taxRate,
                        TaxAmount = taxAmount,
                        Amount = amount,
                        CostRate = costRate,
                        CostAmount = costAmount,
                        CompanyId = store.CompanyId
                    });

                    db.StockMovements.Add(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        StockId = lineStock.Id,
                        ProductId = lineStock.ProductId,
                        Barcode = lineStock.Barcode,
                        MovementType = documentType == NonGstGoodsDocumentType.Purchase ? "NonGstPurchaseIn" : "NonGstSaleOut",
                        QuantityIn = documentType == NonGstGoodsDocumentType.Purchase ? line.Quantity : 0,
                        QuantityOut = documentType == NonGstGoodsDocumentType.Sale ? line.Quantity : 0,
                        CostPrice = lineStock.CostPrice,
                        MRP = lineStock.MRP,
                        TaxRate = 0,
                        HSNCode = null,
                        SourceType = documentType == NonGstGoodsDocumentType.Purchase ? "NonGstPurchase" : "NonGstSale",
                        SourceId = document.Id,
                        SourceNumber = document.DocumentNumber,
                        Remarks = documentType == NonGstGoodsDocumentType.Purchase ? "Non-GST/out-of-scope purchase memo" : "Non-GST/out-of-scope sale cash memo",
                        OnDate = document.OnDate,
                        CompanyId = store.CompanyId,
                        StoreGroupId = store.StoreGroupId,
                        StoreId = store.Id
                    });
                }

                document.GrossAmount = Money(gross);
                document.DiscountAmount = Money(discount);
                document.NetAmount = Money(gross - discount);
                document.PaidAmount = Money(request.PaidAmount ?? document.NetAmount);
                if (document.PaidAmount < 0 || document.PaidAmount > document.NetAmount)
                {
                    return Results.BadRequest(new { message = "Paid amount must be between zero and the document net amount." });
                }
                document.BalanceAmount = Money(document.NetAmount - document.PaidAmount);

                if (db.ChangeTracker.Entries<Stock>()
                    .Any(entry => entry.Entity.IsOFB && entry.Entity.CurrentStock < 0))
                {
                    return Results.BadRequest(new { message = "The edited purchase quantity cannot be lower than quantity already sold." });
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var response = new NonGstGoodsResponse(
                    document.Id,
                    document.DocumentNumber,
                    document.DocumentType.ToString(),
                    document.OnDate,
                    document.PartyName,
                    document.GrossAmount,
                    document.DiscountAmount,
                    document.NetAmount,
                    document.PaidAmount,
                    document.BalanceAmount,
                    documentType == NonGstGoodsDocumentType.Purchase
                        ? "Off Book purchase saved. Stock and payment remain outside regular purchase, GST and accounting books."
                        : "Off Book sale saved. Stock and receipt remain outside regular sales, GST and accounting books.");
                return documentId.HasValue
                    ? Results.Ok(response)
                    : Results.Created($"/api/non-gst-goods/documents/{document.Id}", response);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.BadRequest(new { message = ex.Message });
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private sealed record StoreResolveResult(Store? Store, string? ErrorMessage);

    private static async Task<IResult> GetDocumentAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Off Book document was not found." });
        }

        var items = await db.NonGstGoodsItems.AsNoTracking()
            .Where(item => item.DocumentId == id)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id,
                item.StockId,
                item.ProductId,
                item.ProductName,
                item.Barcode,
                item.Quantity,
                item.Rate,
                item.DiscountAmount,
                item.CostRate,
                item.Amount
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            document.Id,
            document.DocumentNumber,
            document.OnDate,
            document.DocumentType,
            document.PartyName,
            document.VendorId,
            document.CustomerId,
            document.PaymentMode,
            document.ReferenceNumber,
            document.GrossAmount,
            document.DiscountAmount,
            document.NetAmount,
            document.PaidAmount,
            document.BalanceAmount,
            document.Remarks,
            document.StoreId,
            Items = items
        });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var document = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments, context)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (document is null)
            {
                return Results.NotFound(new { message = "Off Book document was not found." });
            }

            await ReverseDocumentStockAsync(document, db, cancellationToken);
            if (db.ChangeTracker.Entries<Stock>().Any(entry => entry.Entity.IsOFB && entry.Entity.CurrentStock < 0))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Results.BadRequest(new { message = "This purchase cannot be deleted because some of its stock has already been sold." });
            }

            db.NonGstGoodsDocuments.Remove(document);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static async Task ReverseDocumentStockAsync(
        NonGstGoodsDocument document,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var lines = await db.NonGstGoodsItems
            .Where(item => item.DocumentId == document.Id)
            .ToListAsync(cancellationToken);
        var stockIds = lines.Where(item => item.StockId.HasValue).Select(item => item.StockId!.Value).Distinct().ToArray();
        var stocks = await db.Stocks.Where(item => stockIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var line in lines)
        {
            if (!line.StockId.HasValue || !stocks.TryGetValue(line.StockId.Value, out var stock))
            {
                continue;
            }

            if (document.DocumentType == NonGstGoodsDocumentType.Purchase)
            {
                stock.PurchaseQty -= line.Quantity;
            }
            else
            {
                stock.SoldQty = Math.Max(0, stock.SoldQty - line.Quantity);
                stock.SoldValue = Math.Max(0, stock.SoldValue - line.Amount);
            }
        }

        var movements = await db.StockMovements
            .Where(item => item.SourceId == document.Id
                && (item.SourceType == "NonGstPurchase" || item.SourceType == "NonGstSale"))
            .ToListAsync(cancellationToken);
        db.StockMovements.RemoveRange(movements);
        db.NonGstGoodsItems.RemoveRange(lines);
    }

    private static async Task<StoreResolveResult> ResolveDocumentStoreAsync(NonGstGoodsDocumentType documentType, NonGstGoodsRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var requestedStoreIds = request.Items
            .Where(item => item.StoreId.HasValue && item.StoreId.Value != Guid.Empty)
            .Select(item => item.StoreId!.Value)
            .Distinct()
            .ToArray();
        if (requestedStoreIds.Length > 1)
        {
            return new StoreResolveResult(null, "All Off Book document items must belong to the same store.");
        }

        Guid? storeId = requestedStoreIds.FirstOrDefault();
        if (storeId == Guid.Empty)
        {
            storeId = null;
        }
        if (!storeId.HasValue)
        {
            var stockIds = request.Items
                .Where(item => item.StockId.HasValue && item.StockId.Value != Guid.Empty)
                .Select(item => item.StockId!.Value)
                .Distinct()
                .ToArray();
            if (stockIds.Length > 0)
            {
                var stockStoreIds = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
                    .Where(item => stockIds.Contains(item.Id) && item.IsOFB)
                    .Select(item => item.StoreId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                if (stockStoreIds.Count != 1)
                {
                    return new StoreResolveResult(null, "All selected Off Book stock items must belong to one permitted store.");
                }
                storeId = stockStoreIds[0];
            }
        }

        if (!storeId.HasValue)
        {
            storeId = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .OrderBy(item => item.Name)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!storeId.HasValue)
        {
            return new StoreResolveResult(null, "No store is available for this operation.");
        }

        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == storeId.Value, cancellationToken);
        return store is null
            ? new StoreResolveResult(null, "Selected store is outside your access scope.")
            : new StoreResolveResult(store, null);
    }

    private static async Task<NonGstReportDto> ReportAsync(HttpContext context, GarmetixDbContext db, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var start = (from ?? DateTime.Today.AddDays(-30)).Date;
        var endExclusive = (to ?? DateTime.Today).Date.AddDays(1);
        var rows = await BuildDocumentQuery(context, db, start, endExclusive, 1000).ToListAsync(cancellationToken);
        var purchaseRows = rows.Where(item => item.DocumentType == NonGstGoodsDocumentType.Purchase.ToString()).ToList();
        var saleRows = rows.Where(item => item.DocumentType == NonGstGoodsDocumentType.Sale.ToString()).ToList();
        var stockRows = await BuildCurrentStockRowsAsync(context, db, cancellationToken);

        return new NonGstReportDto(
            start,
            endExclusive.AddDays(-1),
            purchaseRows.Count,
            purchaseRows.Sum(item => item.Quantity),
            purchaseRows.Sum(item => item.NetAmount),
            saleRows.Count,
            saleRows.Sum(item => item.Quantity),
            saleRows.Sum(item => item.NetAmount),
            rows.Sum(item => item.DiscountAmount),
            saleRows.Sum(item => item.CostAmount),
            saleRows.Sum(item => item.ProfitAmount),
            stockRows.Sum(item => item.CurrentStock),
            stockRows.Sum(item => item.StockValue),
            rows,
            stockRows);
    }

    private static IQueryable<NonGstDocumentRowDto> BuildDocumentQuery(HttpContext context, GarmetixDbContext db, DateTime start, DateTime endExclusive, int take)
        => WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
            .Where(item => item.OnDate >= start && item.OnDate < endExclusive)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.DocumentNumber)
            .Take(take)
            .Select(item => new NonGstDocumentRowDto(
                item.Id,
                item.DocumentNumber,
                item.DocumentType == NonGstGoodsDocumentType.Purchase ? "Purchase" : "Sale",
                item.OnDate,
                item.PartyName,
                db.NonGstGoodsItems.Count(line => line.DocumentId == item.Id),
                db.NonGstGoodsItems.Where(line => line.DocumentId == item.Id).Sum(line => (decimal?)line.Quantity) ?? 0,
                item.GrossAmount,
                item.DiscountAmount,
                item.NetAmount,
                item.PaidAmount,
                item.BalanceAmount,
                item.BalanceAmount <= 0 ? "Paid" : item.PaidAmount > 0 ? "Partially Paid" : "Due",
                db.NonGstGoodsItems.Where(line => line.DocumentId == item.Id).Sum(line => (decimal?)line.CostAmount) ?? 0,
                item.DocumentType == NonGstGoodsDocumentType.Sale
                    ? item.NetAmount - (db.NonGstGoodsItems.Where(line => line.DocumentId == item.Id).Sum(line => (decimal?)line.CostAmount) ?? 0)
                    : 0,
                item.Remarks));

    private static async Task<IReadOnlyList<NonGstReportStockRowDto>> BuildCurrentStockRowsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var storeNames = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var rows = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => item.IsOFB)
            .Include(item => item.Product)
            .OrderBy(item => item.Product != null ? item.Product.Name : item.Barcode)
            .ThenBy(item => item.Barcode)
            .Select(item => new
            {
                item.Id,
                item.ProductId,
                ProductName = item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                item.StoreId,
                item.PurchaseQty,
                item.SoldQty,
                CurrentStock = item.PurchaseQty - item.SoldQty,
                item.CostPrice,
                item.MRP
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new NonGstReportStockRowDto(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.Barcode,
                item.StoreId,
                storeNames.GetValueOrDefault(item.StoreId, "Store"),
                item.PurchaseQty,
                item.SoldQty,
                item.CurrentStock,
                item.CostPrice,
                item.MRP,
                Money(item.CurrentStock * item.CostPrice)))
            .ToList();
    }

    private static async Task<IResult> PrintAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var dto = await BuildPrintDtoAsync(id, context, db, cancellationToken);
        return dto is null
            ? Results.NotFound(new { message = "Off Book document was not found." })
            : Results.Ok(dto);
    }

    private static async Task<IResult> DownloadPdfAsync(
        Guid id,
        string? format,
        bool? reprint,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var dto = await BuildPrintDtoAsync(id, context, db, cancellationToken);
        if (dto is null)
        {
            return Results.NotFound(new { message = "Off Book document was not found." });
        }

        var pdf = NonGstGoodsPdfDocument.Build(dto, format ?? "a4", reprint == true);
        var safeNumber = Regex.Replace(dto.DocumentNumber, @"[^A-Za-z0-9_-]+", "-").Trim('-');
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "off-book-document")}.pdf");
    }

    private static async Task<NonGstPrintDto?> BuildPrintDtoAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == document.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == document.StoreId, cancellationToken);
        var lines = await db.NonGstGoodsItems.AsNoTracking()
            .Where(item => item.DocumentId == document.Id)
            .OrderBy(item => item.ProductName)
            .ThenBy(item => item.Barcode)
            .ToListAsync(cancellationToken);

        var serial = 0;
        var items = lines.Select(line => new NonGstPrintItemDto(
                ++serial,
                line.ProductName,
                line.Barcode,
                line.Quantity,
                line.Rate,
                line.GrossAmount == 0 ? Money(line.Quantity * line.Rate) : line.GrossAmount,
                line.DiscountAmount,
                line.TaxableAmount == 0 ? line.Amount : line.TaxableAmount,
                line.TaxRate,
                line.TaxAmount,
                line.Amount,
                line.CostRate,
                line.CostAmount,
                document.DocumentType == NonGstGoodsDocumentType.Sale ? line.Amount - line.CostAmount : 0))
            .ToList();

        var dto = new NonGstPrintDto(
            document.Id,
            document.DocumentNumber,
            document.DocumentType == NonGstGoodsDocumentType.Purchase ? "Purchase Memo" : "Cash Memo",
            document.OnDate,
            document.PartyName,
            document.ReferenceNumber,
            document.PaymentMode.ToString(),
            document.Remarks,
            company?.Name ?? "Garmetix",
            store?.Name ?? "Store",
            store?.Address ?? string.Empty,
            store?.ContactNumber,
            store?.Email,
            "OFF BOOK document. It does not create a regular sale, purchase, GST, ledger, journal, bank or voucher entry.",
            document.GrossAmount,
            document.DiscountAmount,
            items.Sum(item => item.TaxableAmount),
            items.Sum(item => item.TaxAmount),
            document.NetAmount,
            document.PaidAmount,
            document.BalanceAmount,
            document.BalanceAmount <= 0 ? "Paid" : document.PaidAmount > 0 ? "Partially Paid" : "Due",
            items.Sum(item => item.CostAmount),
            document.DocumentType == NonGstGoodsDocumentType.Sale ? document.NetAmount - items.Sum(item => item.CostAmount) : 0,
            items);

        return dto;
    }

    private static async Task<Stock> ResolveOrCreateNonGstStockAsync(NonGstGoodsDocumentType documentType, NonGstGoodsItemRequest line, Store store, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (line.StockId.HasValue && line.StockId.Value != Guid.Empty)
        {
            var selectedStock = await WorkspaceScope.ApplyTo(db.Stocks.Include(item => item.Product), context)
                .FirstOrDefaultAsync(item => item.Id == line.StockId.Value, cancellationToken);
            if (selectedStock is null)
            {
                throw new InvalidOperationException("Selected Non-GST stock was not found.");
            }
            if (!selectedStock.IsOFB || selectedStock.StoreId != store.Id)
            {
                throw new InvalidOperationException("Selected stock does not belong to this Off Book document store.");
            }
            return selectedStock;
        }

        if (documentType == NonGstGoodsDocumentType.Sale)
        {
            throw new InvalidOperationException("Sale requires an existing Non-GST stock row.");
        }

        var barcode = Clean(line.Barcode) ?? GenerateNonGstBarcode(store);
        var productName = Clean(line.ProductName) ?? $"Non-GST Item {barcode}";
        var product = await WorkspaceScope.ApplyTo(db.Products, context)
            .FirstOrDefaultAsync(item => item.Barcode == barcode, cancellationToken);
        var (categoryId, subCategoryId) = await EnsureNonGstProductCategoryAsync(db, store.CompanyId, cancellationToken);
        var zeroTax = await EnsureZeroTaxAsync(db, cancellationToken);
        if (product is null)
        {
            product = new Product
            {
                Id = Guid.NewGuid(),
                Name = productName,
                Barcode = barcode,
                MRP = line.MRP ?? line.Rate,
                TaxRate = 0,
                Unit = Unit.Pcs,
                TaxType = TaxType.GST,
                ProductType = ProductType.Others,
                ProductGroup = ProductGroup.Others,
                ProductCategoryId = categoryId,
                ProductSubCategoryId = subCategoryId,
                CompanyId = store.CompanyId,
                StoreGroupId = store.StoreGroupId,
                Descriptions = "Non-GST/out-of-scope stock item"
            };
            db.Products.Add(product);
        }

        var stock = await db.Stocks.Include(item => item.Product)
            .FirstOrDefaultAsync(item => item.CompanyId == store.CompanyId && item.StoreId == store.Id && item.Barcode == barcode && item.IsOFB, cancellationToken);
        if (stock is null)
        {
            stock = new Stock
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Product = product,
                Barcode = barcode,
                HSNCode = null,
                Unit = Unit.Pcs,
                PurchaseQty = 0,
                CostPrice = line.CostPrice ?? line.Rate,
                SoldQty = 0,
                MRP = line.MRP ?? line.Rate,
                TaxRate = 0,
                TaxType = TaxType.GST,
                TaxId = zeroTax.Id,
                BrandedProduct = false,
                SoldValue = 0,
                StockType = StockType.Unbilled,
                IsOFB = true,
                CompanyId = store.CompanyId,
                StoreGroupId = store.StoreGroupId,
                StoreId = store.Id
            };
            db.Stocks.Add(stock);
        }
        return stock;
    }

    private static string GenerateNonGstBarcode(Store store)
        => $"NG-{store.StoreCode ?? "STORE"}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..36];

    private static async Task<(Guid CategoryId, Guid SubCategoryId)> EnsureNonGstProductCategoryAsync(GarmetixDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var category = await db.ProductCategories.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Name == "Non-GST Goods", cancellationToken);
        if (category is null)
        {
            category = new InventoryProductCategory
            {
                Id = Guid.NewGuid(),
                Name = "Non-GST Goods",
                ProductGroup = ProductGroup.Others,
                IsActive = true,
                CompanyId = companyId
            };
            db.ProductCategories.Add(category);
        }

        var subCategory = await db.ProductSubCategories.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.CategoryId == category.Id && item.Name == "Out-of-Scope", cancellationToken);
        if (subCategory is null)
        {
            subCategory = new ProductSubCategory
            {
                Id = Guid.NewGuid(),
                Name = "Out-of-Scope",
                CategoryId = category.Id,
                CompanyId = companyId
            };
            db.ProductSubCategories.Add(subCategory);
        }

        return (category.Id, subCategory.Id);
    }

    private static async Task<Tax> EnsureZeroTaxAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var tax = await db.Taxes.FirstOrDefaultAsync(item => item.Name == "Non GST 0%" && item.CompositeRate == 0, cancellationToken);
        if (tax is null)
        {
            tax = new Tax
            {
                Id = Guid.NewGuid(),
                Name = "Non GST 0%",
                CompositeRate = 0,
                TaxType = TaxType.GST
            };
            db.Taxes.Add(tax);
        }

        return tax;
    }

    private static async Task<string> ResolvePartyNameAsync(NonGstGoodsDocumentType type, NonGstGoodsRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (type == NonGstGoodsDocumentType.Purchase && request.VendorId.HasValue && request.VendorId.Value != Guid.Empty)
        {
            var vendor = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.VendorId.Value, cancellationToken);
            if (vendor is not null) return vendor.Name;
        }
        if (type == NonGstGoodsDocumentType.Sale && request.CustomerId.HasValue && request.CustomerId.Value != Guid.Empty)
        {
            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.CustomerId.Value, cancellationToken);
            if (customer is not null) return customer.Name;
        }
        return Clean(request.PartyName) ?? (type == NonGstGoodsDocumentType.Purchase ? "Non-GST Supplier" : "Walk-in Customer");
    }

    private static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
