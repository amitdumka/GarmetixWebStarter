using Garmetix.Api.Auth;
using Garmetix.Api.Accounting;
using Garmetix.Api.Numbering;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Inventory;

public static class StockOperationEndpoints
{
    public static RouteGroupBuilder MapInventoryStockOperationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/stock-operations")
            .WithTags("Inventory Stock Operations")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("/options", OptionsAsync);
        group.MapGet("/movements", MovementsAsync);
        group.MapGet("/documents", DocumentsAsync);
        group.MapGet("/documents/{id:guid}", DocumentAsync);
        group.MapGet("/valuation", ValuationAsync);
        group.MapPost("/adjustment", CreateAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/transfer", CreateTransferAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/physical-count", CreatePhysicalCountAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/write-off", CreateWriteOffAsync).RequireAuthorization(GarmetixPolicies.Edit);

        return group;
    }

    private static async Task<StockOperationOptionsDto> OptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var stores = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new StockOperationStoreOptionDto(item.Id, item.Name, item.CompanyId, item.StoreGroupId))
            .ToListAsync(cancellationToken);

        var storeNameById = stores.ToDictionary(item => item.Id, item => item.Name);
        var products = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => !item.IsOFB)
            .Include(item => item.Product)
            .OrderBy(item => item.Product != null ? item.Product.Name : item.Barcode)
            .ThenBy(item => item.Barcode)
            .Select(item => new StockOperationProductOptionDto(
                item.ProductId,
                item.Id,
                item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                item.HSNCode ?? (item.Product != null ? item.Product.HSNCode : null),
                item.Unit,
                item.PurchaseQty - item.SoldQty,
                item.CostPrice,
                item.MRP,
                item.TaxRate,
                item.TaxType,
                item.TaxId,
                item.CompanyId,
                item.StoreGroupId,
                item.StoreId,
                string.Empty,
                string.Empty))
            .ToListAsync(cancellationToken);

        var enrichedProducts = products.Select(item => item with
            {
                StoreName = storeNameById.GetValueOrDefault(item.StoreId, "Store"),
                Label = $"{item.ProductName} | {item.Barcode} | {storeNameById.GetValueOrDefault(item.StoreId, "Store")} | Qty {item.CurrentStock:0.##}"
            })
            .ToList();

        var recentMovements = await MovementQuery(context, db, 25)
            .ToListAsync(cancellationToken);

        return new StockOperationOptionsDto(enrichedProducts, stores, recentMovements);
    }

    private static async Task<IReadOnlyList<StockMovementRowDto>> MovementsAsync(HttpContext context, GarmetixDbContext db, int? take, CancellationToken cancellationToken)
    {
        var rowCount = Math.Clamp(take ?? 50, 1, 200);
        return await MovementQuery(context, db, rowCount)
            .ToListAsync(cancellationToken);
    }

    private static async Task<StockValuationSummaryDto> ValuationAsync(
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        int? take,
        CancellationToken cancellationToken)
    {
        var stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => !item.IsOFB)
            .Include(item => item.Product)
            .OrderBy(item => item.Product != null ? item.Product.Name : item.Barcode)
            .Take(Math.Clamp(take ?? 250, 1, 500))
            .ToListAsync(cancellationToken);
        var snapshots = await stockLedger.GetSnapshotsAsync(stocks, cancellationToken);
        var storeNames = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = stocks.Select(stock =>
        {
            var snapshot = snapshots[stock.Id];
            var projected = stock.PurchaseQty - stock.SoldQty;
            var matches = Math.Abs(snapshot.Quantity - projected) <= 0.01m
                && Math.Abs(snapshot.AverageCost - stock.CostPrice) <= 0.01m;
            return new StockValuationRowDto(
                stock.Id,
                stock.ProductId,
                stock.Product?.Name ?? stock.Barcode,
                stock.Barcode,
                storeNames.GetValueOrDefault(stock.StoreId, "Store"),
                snapshot.Quantity,
                projected,
                snapshot.AverageCost,
                snapshot.InventoryValue,
                snapshot.LastMovementAt,
                matches ? "Matched" : "Rebuild Required");
        }).ToList();

        return new StockValuationSummaryDto(
            "WeightedAverage",
            rows.Count,
            rows.Sum(item => item.LedgerQuantity),
            rows.Sum(item => item.InventoryValue),
            rows.Count(item => item.ProjectionStatus != "Matched"),
            rows);
    }

    private static async Task<IReadOnlyList<StockOperationDocumentRowDto>> DocumentsAsync(
        HttpContext context,
        GarmetixDbContext db,
        int? take,
        CancellationToken cancellationToken)
    {
        var rowCount = Math.Clamp(take ?? 100, 1, 250);
        return await WorkspaceScope.ApplyTo(db.StockOperationDocuments.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(rowCount)
            .Select(item => new StockOperationDocumentRowDto(
                item.Id,
                item.DocumentNumber,
                item.OnDate,
                item.OperationType,
                item.Status,
                item.FromStoreName,
                item.ToStoreName,
                item.TotalQuantity,
                item.TotalCostValue,
                item.TotalMrpValue,
                item.ItemCount,
                item.AccountingStatus,
                item.JournalEntryId,
                item.JournalEntryId.HasValue
                    ? db.JournalEntries.Where(entry => entry.Id == item.JournalEntryId.Value).Select(entry => entry.EntryNumber).FirstOrDefault()
                    : null,
                item.Reason))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> DocumentAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await WorkspaceScope.ApplyTo(db.StockOperationDocuments.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null)
        {
            return Results.NotFound(new { message = "Stock operation document was not found." });
        }

        var items = await db.StockOperationItems.AsNoTracking()
            .Where(item => item.StockOperationDocumentId == document.Id)
            .OrderBy(item => item.ProductName)
            .Select(item => new StockOperationItemDto(
                item.Id,
                item.ProductId,
                item.StockId,
                item.DestinationStockId,
                item.ProductName,
                item.Barcode,
                item.HSNCode,
                item.Unit.ToString(),
                item.SystemQuantity,
                item.CountedQuantity,
                item.QuantityIn,
                item.QuantityOut,
                item.QuantityDifference,
                item.FromQuantityBefore,
                item.FromQuantityAfter,
                item.ToQuantityBefore,
                item.ToQuantityAfter,
                item.CostPrice,
                item.MRP,
                item.CostValue,
                item.MrpValue,
                item.OutMovementId,
                item.InMovementId,
                item.Reason))
            .ToListAsync(cancellationToken);
        var journalEntryNumber = document.JournalEntryId.HasValue
            ? await db.JournalEntries.AsNoTracking()
                .Where(entry => entry.Id == document.JournalEntryId.Value)
                .Select(entry => entry.EntryNumber)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return Results.Ok(new StockOperationDocumentDetailDto(
            document.Id,
            document.DocumentNumber,
            document.OnDate,
            document.OperationType,
            document.Status,
            document.FromStoreId,
            document.FromStoreName,
            document.ToStoreId,
            document.ToStoreName,
            document.TotalQuantity,
            document.TotalCostValue,
            document.TotalMrpValue,
            document.ItemCount,
            document.AccountingStatus,
            document.JournalEntryId,
            journalEntryNumber,
            document.Reason,
            document.PostedAt,
            items));
    }

    private static IQueryable<StockMovementRowDto> MovementQuery(HttpContext context, GarmetixDbContext db, int take)
    {
        // Keep ordering on entity fields before projecting into StockMovementRowDto.
        // EF Core cannot translate OrderBy(new StockMovementRowDto(...).OnDate).
        return WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(item =>
                item.SourceType != "NonGstPurchase" &&
                item.SourceType != "NonGstSale" &&
                (!item.StockId.HasValue || !db.Stocks.Any(stock => stock.Id == item.StockId.Value && stock.IsOFB)))
            .Include(item => item.Product)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .Select(item => new StockMovementRowDto(
                item.Id,
                item.OnDate,
                item.MovementType,
                item.Product != null ? item.Product.Name : item.Barcode,
                item.Barcode,
                db.Stores.Where(store => store.Id == item.StoreId).Select(store => store.Name).FirstOrDefault() ?? "Store",
                item.QuantityIn,
                item.QuantityOut,
                item.MRP,
                item.CostPrice,
                item.QuantityAfter,
                item.AverageCostAfter,
                item.InventoryValueAfter,
                item.ValuationMethod,
                item.SourceNumber,
                item.Remarks));
    }

    private static async Task<IResult> CreateAdjustmentAsync(
        StockAdjustmentRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        StockLedgerService stockLedger,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.StockId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select a stock item before posting adjustment." });
        }

        if (request.Quantity <= 0)
        {
            return Results.BadRequest(new { message = "Adjustment quantity must be greater than zero." });
        }

        var direction = (request.Direction ?? string.Empty).Trim().ToLowerInvariant();
        if (direction is not ("increase" or "in" or "add" or "decrease" or "out" or "reduce"))
        {
            return Results.BadRequest(new { message = "Adjustment direction must be Increase or Decrease." });
        }

        var increase = direction is "increase" or "in" or "add";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stock = await LoadScopedStockAsync(request.StockId, context, db, cancellationToken);
        if (stock is null)
        {
            return Results.NotFound();
        }

        await DocumentNumberGenerator.LockStockKeyAsync(db, stock.CompanyId, stock.StoreGroupId, stock.StoreId, stock.ProductId, stock.Barcode, cancellationToken);

        StockQuantityChange change;
        try
        {
            var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            change = StockOperationCalculator.Adjustment(snapshot.Quantity, request.Quantity, increase);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        var onDate = DateTime.Now;
        var operationNumber = await documentNumbers.NextStockAdjustmentAsync(stock.CompanyId, stock.StoreGroupId, stock.StoreId, onDate, cancellationToken);
        var reason = Clean(request.Reason) ?? "Manual stock adjustment";
        var storeName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == stock.StoreId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Store";
        var document = CreateDocument(
            operationNumber,
            onDate,
            "Adjustment",
            stock,
            storeName,
            null,
            null,
            request.Quantity,
            reason);
        db.StockOperationDocuments.Add(document);

        var movement = CreateMovement(
            stock,
            increase ? "StockAdjustmentIn" : "StockAdjustmentOut",
            change.QuantityIn,
            change.QuantityOut,
            document,
            reason);
        var posting = await stockLedger.PostAsync(stock, movement, cancellationToken);
        document.TotalCostValue = Math.Abs(posting.CostImpact);
        db.StockOperationItems.Add(CreateItem(
            document,
            stock,
            stock.Id,
            null,
            stock.StoreId,
            null,
            posting.Before.Quantity,
            null,
            new StockQuantityChange(
                posting.Before.Quantity,
                change.QuantityIn,
                change.QuantityOut,
                posting.After.Quantity,
                posting.After.Quantity - posting.Before.Quantity),
            null,
            null,
            increase ? null : movement.Id,
            increase ? movement.Id : null,
            reason));
        var accountingResult = await accounting.PostStockOperationAsync(
            document,
            increase ? StockOperationAccountingKind.Excess : StockOperationAccountingKind.Shortage,
            posting.CostImpact,
            cancellationToken);
        ApplyAccountingResult(document, accountingResult);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockOperationResponse(
            document.Id,
            operationNumber,
            stock.ProductId,
            stock.Barcode,
            stock.StoreId,
            change.QuantityIn,
            change.QuantityOut,
            stock.CurrentStock,
            increase ? "StockAdjustmentIn" : "StockAdjustmentOut",
            "Stock adjustment posted."));
    }

    private static async Task<IResult> CreatePhysicalCountAsync(
        PhysicalStockCountRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        StockLedgerService stockLedger,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.StockId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select a stock item before posting physical count." });
        }

        if (request.CountedQuantity < 0)
        {
            return Results.BadRequest(new { message = "Physical count cannot be negative." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stock = await LoadScopedStockAsync(request.StockId, context, db, cancellationToken);
        if (stock is null)
        {
            return Results.NotFound();
        }

        await DocumentNumberGenerator.LockStockKeyAsync(db, stock.CompanyId, stock.StoreGroupId, stock.StoreId, stock.ProductId, stock.Barcode, cancellationToken);

        StockQuantityChange change;
        try
        {
            var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            change = StockOperationCalculator.PhysicalCount(snapshot.Quantity, request.CountedQuantity);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        var onDate = DateTime.Now;
        var operationNumber = await documentNumbers.NextPhysicalStockCountAsync(stock.CompanyId, stock.StoreGroupId, stock.StoreId, onDate, cancellationToken);
        var reason = Clean(request.Reason) ?? $"Physical count set to {request.CountedQuantity:0.##}";
        var storeName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == stock.StoreId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Store";
        var document = CreateDocument(
            operationNumber,
            onDate,
            "PhysicalCount",
            stock,
            storeName,
            null,
            null,
            Math.Abs(change.Difference),
            reason);
        document.Status = change.Difference == 0 ? "Verified" : "Posted";
        db.StockOperationDocuments.Add(document);

        StockMovement? movement = null;
        StockLedgerPosting? posting = null;
        if (change.Difference != 0)
        {
            movement = CreateMovement(
                stock,
                change.Difference > 0 ? "PhysicalCountGain" : "PhysicalCountLoss",
                change.QuantityIn,
                change.QuantityOut,
                document,
                reason);
            posting = await stockLedger.PostAsync(stock, movement, cancellationToken);
            document.TotalCostValue = Math.Abs(posting.CostImpact);
        }

        db.StockOperationItems.Add(CreateItem(
            document,
            stock,
            stock.Id,
            null,
            stock.StoreId,
            null,
            change.BeforeQuantity,
            request.CountedQuantity,
            posting is null
                ? change
                : new StockQuantityChange(
                    posting.Before.Quantity,
                    change.QuantityIn,
                    change.QuantityOut,
                    posting.After.Quantity,
                    posting.After.Quantity - posting.Before.Quantity),
            null,
            null,
            change.Difference < 0 ? movement?.Id : null,
            change.Difference > 0 ? movement?.Id : null,
            reason));
        if (posting is null)
        {
            document.AccountingStatus = "Not Required";
        }
        else
        {
            var accountingResult = await accounting.PostStockOperationAsync(
                document,
                change.Difference > 0 ? StockOperationAccountingKind.Excess : StockOperationAccountingKind.Shortage,
                posting.CostImpact,
                cancellationToken);
            ApplyAccountingResult(document, accountingResult);
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockOperationResponse(
            document.Id,
            operationNumber,
            stock.ProductId,
            stock.Barcode,
            stock.StoreId,
            change.QuantityIn,
            change.QuantityOut,
            stock.CurrentStock,
            change.Difference > 0 ? "PhysicalCountGain" : change.Difference < 0 ? "PhysicalCountLoss" : "PhysicalCountNoChange",
            change.Difference == 0 ? "Physical count verified. No quantity movement was required." : "Physical stock count posted."));
    }

    private static async Task<IResult> CreateWriteOffAsync(
        StockWriteOffRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        StockLedgerService stockLedger,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.StockId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select a stock item before posting write-off." });
        }

        if (request.Quantity <= 0)
        {
            return Results.BadRequest(new { message = "Write-off quantity must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stock = await LoadScopedStockAsync(request.StockId, context, db, cancellationToken);
        if (stock is null)
        {
            return Results.NotFound();
        }

        await DocumentNumberGenerator.LockStockKeyAsync(
            db,
            stock.CompanyId,
            stock.StoreGroupId,
            stock.StoreId,
            stock.ProductId,
            stock.Barcode,
            cancellationToken);

        StockQuantityChange change;
        try
        {
            var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            change = StockOperationCalculator.Adjustment(snapshot.Quantity, request.Quantity, false);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        var onDate = DateTime.Now;
        var operationNumber = await documentNumbers.NextStockWriteOffAsync(
            stock.CompanyId,
            stock.StoreGroupId,
            stock.StoreId,
            onDate,
            cancellationToken);
        var reason = Clean(request.Reason) ?? "Damaged or unusable stock write-off";
        var storeName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == stock.StoreId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Store";
        var document = CreateDocument(
            operationNumber,
            onDate,
            "WriteOff",
            stock,
            storeName,
            null,
            null,
            request.Quantity,
            reason);
        db.StockOperationDocuments.Add(document);

        var movement = CreateMovement(stock, "StockWriteOff", 0, request.Quantity, document, reason);
        var posting = await stockLedger.PostAsync(stock, movement, cancellationToken);
        document.TotalCostValue = Math.Abs(posting.CostImpact);
        db.StockOperationItems.Add(CreateItem(
            document,
            stock,
            stock.Id,
            null,
            stock.StoreId,
            null,
            posting.Before.Quantity,
            null,
            new StockQuantityChange(
                posting.Before.Quantity,
                0,
                request.Quantity,
                posting.After.Quantity,
                -request.Quantity),
            null,
            null,
            movement.Id,
            null,
            reason));
        var accountingResult = await accounting.PostStockOperationAsync(
            document,
            StockOperationAccountingKind.WriteOff,
            posting.CostImpact,
            cancellationToken);
        ApplyAccountingResult(document, accountingResult);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockOperationResponse(
            document.Id,
            operationNumber,
            stock.ProductId,
            stock.Barcode,
            stock.StoreId,
            0,
            request.Quantity,
            stock.CurrentStock,
            "StockWriteOff",
            "Stock write-off posted."));
    }

    private static async Task<IResult> CreateTransferAsync(
        StockTransferRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        StockLedgerService stockLedger,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.FromStockId == Guid.Empty || request.ToStoreId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select source stock and destination store before transfer." });
        }

        if (request.Quantity <= 0)
        {
            return Results.BadRequest(new { message = "Transfer quantity must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var fromStock = await LoadScopedStockAsync(request.FromStockId, context, db, cancellationToken);
        if (fromStock is null)
        {
            return Results.NotFound();
        }

        var toStore = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == request.ToStoreId, cancellationToken);
        if (toStore is null)
        {
            return Results.BadRequest(new { message = "Destination store is outside your access scope." });
        }

        if (toStore.CompanyId != fromStock.CompanyId)
        {
            return Results.BadRequest(new { message = "Destination store must belong to the same company." });
        }

        if (fromStock.StoreId == toStore.Id)
        {
            return Results.BadRequest(new { message = "Source and destination store cannot be same." });
        }

        StockQuantityChange sourceChange;
        try
        {
            var snapshot = await stockLedger.GetSnapshotAsync(fromStock, cancellationToken);
            sourceChange = StockOperationCalculator.Transfer(snapshot.Quantity, request.Quantity);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        await DocumentNumberGenerator.LockStockKeyAsync(db, fromStock.CompanyId, fromStock.StoreGroupId, fromStock.StoreId, fromStock.ProductId, fromStock.Barcode, cancellationToken);
        await DocumentNumberGenerator.LockStockKeyAsync(db, fromStock.CompanyId, toStore.StoreGroupId, toStore.Id, fromStock.ProductId, fromStock.Barcode, cancellationToken);

        var toStock = await db.Stocks.FirstOrDefaultAsync(item =>
            item.CompanyId == fromStock.CompanyId &&
            item.StoreGroupId == toStore.StoreGroupId &&
            item.StoreId == toStore.Id &&
            item.ProductId == fromStock.ProductId &&
            item.Barcode == fromStock.Barcode &&
            !item.IsOFB,
            cancellationToken);

        if (toStock is null)
        {
            toStock = new Stock
            {
                ProductId = fromStock.ProductId,
                Barcode = fromStock.Barcode,
                HSNCode = fromStock.HSNCode,
                Unit = fromStock.Unit,
                PurchaseQty = 0,
                CostPrice = fromStock.CostPrice,
                SoldQty = 0,
                MRP = fromStock.MRP,
                TaxRate = fromStock.TaxRate,
                TaxType = fromStock.TaxType,
                TaxId = fromStock.TaxId,
                BrandedProduct = fromStock.BrandedProduct,
                StockType = fromStock.StockType,
                IsOFB = false,
                CompanyId = fromStock.CompanyId,
                StoreGroupId = toStore.StoreGroupId,
                StoreId = toStore.Id
            };
            db.Stocks.Add(toStock);
        }

        var destinationSnapshot = await stockLedger.GetSnapshotAsync(toStock, cancellationToken);
        var destinationQuantityBefore = destinationSnapshot.Quantity;
        var onDate = DateTime.Now;
        var operationNumber = await documentNumbers.NextStockTransferAsync(fromStock.CompanyId, fromStock.StoreGroupId, fromStock.StoreId, onDate, cancellationToken);
        var reason = Clean(request.Reason) ?? $"Transfer to {toStore.Name}";
        var fromStoreName = await db.Stores.AsNoTracking()
            .Where(item => item.Id == fromStock.StoreId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Source Store";
        var document = CreateDocument(
            operationNumber,
            onDate,
            "Transfer",
            fromStock,
            fromStoreName,
            toStore.Id,
            toStore.Name,
            request.Quantity,
            reason);
        db.StockOperationDocuments.Add(document);

        var outMovement = CreateMovement(fromStock, "StockTransferOut", 0, request.Quantity, document, reason);
        var sourcePosting = await stockLedger.PostAsync(fromStock, outMovement, cancellationToken);
        var inMovement = CreateMovement(toStock, "StockTransferIn", request.Quantity, 0, document, $"Transfer from {fromStoreName}");
        inMovement.CostPrice = sourcePosting.Before.AverageCost;
        var destinationPosting = await stockLedger.PostAsync(toStock, inMovement, cancellationToken);
        document.TotalCostValue = Math.Abs(sourcePosting.CostImpact);
        db.StockOperationItems.Add(CreateItem(
            document,
            fromStock,
            fromStock.Id,
            toStock.Id,
            fromStock.StoreId,
            toStock.StoreId,
            sourcePosting.Before.Quantity,
            null,
            new StockQuantityChange(
                sourcePosting.Before.Quantity,
                0,
                request.Quantity,
                sourcePosting.After.Quantity,
                -request.Quantity),
            destinationQuantityBefore,
            destinationPosting.After.Quantity,
            outMovement.Id,
            inMovement.Id,
            reason));
        var accountingResult = await accounting.PostStockOperationAsync(
            document,
            StockOperationAccountingKind.Transfer,
            sourcePosting.CostImpact,
            cancellationToken);
        ApplyAccountingResult(document, accountingResult);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockTransferResponse(
            document.Id,
            operationNumber,
            fromStock.ProductId,
            fromStock.Barcode,
            fromStock.StoreId,
            toStock.StoreId,
            request.Quantity,
            fromStock.CurrentStock,
            toStock.CurrentStock,
            "Stock transfer posted."));
    }

    private static async Task<Stock?> LoadScopedStockAsync(Guid stockId, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return await WorkspaceScope.ApplyTo(db.Stocks, context)
            .Include(item => item.Product)
            .FirstOrDefaultAsync(item => item.Id == stockId && !item.IsOFB, cancellationToken);
    }

    private static StockOperationDocument CreateDocument(
        string documentNumber,
        DateTime onDate,
        string operationType,
        Stock stock,
        string fromStoreName,
        Guid? toStoreId,
        string? toStoreName,
        decimal quantity,
        string reason)
    {
        var absoluteQuantity = Math.Abs(quantity);
        return new StockOperationDocument
        {
            DocumentNumber = documentNumber,
            OnDate = onDate,
            OperationType = operationType,
            Status = "Posted",
            FromStoreId = stock.StoreId,
            FromStoreName = fromStoreName,
            ToStoreId = toStoreId,
            ToStoreName = toStoreName,
            Reason = reason,
            TotalQuantity = absoluteQuantity,
            TotalCostValue = Math.Round(absoluteQuantity * stock.CostPrice, 2),
            TotalMrpValue = Math.Round(absoluteQuantity * stock.MRP, 2),
            ItemCount = 1,
            PostedAt = onDate,
            CompanyId = stock.CompanyId,
            StoreGroupId = stock.StoreGroupId,
            StoreId = stock.StoreId
        };
    }

    private static StockOperationItem CreateItem(
        StockOperationDocument document,
        Stock stock,
        Guid? stockId,
        Guid? destinationStockId,
        Guid? fromStoreId,
        Guid? toStoreId,
        decimal systemQuantity,
        decimal? countedQuantity,
        StockQuantityChange change,
        decimal? toQuantityBefore,
        decimal? toQuantityAfter,
        Guid? outMovementId,
        Guid? inMovementId,
        string reason)
    {
        var absoluteQuantity = Math.Abs(change.Difference);
        return new StockOperationItem
        {
            StockOperationDocumentId = document.Id,
            ProductId = stock.ProductId,
            StockId = stockId,
            DestinationStockId = destinationStockId,
            ProductName = stock.Product?.Name ?? stock.Barcode,
            Barcode = stock.Barcode,
            HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
            Unit = stock.Unit,
            FromStoreId = fromStoreId,
            ToStoreId = toStoreId,
            SystemQuantity = systemQuantity,
            CountedQuantity = countedQuantity,
            QuantityIn = change.QuantityIn,
            QuantityOut = change.QuantityOut,
            QuantityDifference = change.Difference,
            FromQuantityBefore = change.BeforeQuantity,
            FromQuantityAfter = change.AfterQuantity,
            ToQuantityBefore = toQuantityBefore,
            ToQuantityAfter = toQuantityAfter,
            CostPrice = stock.CostPrice,
            MRP = stock.MRP,
            CostValue = Math.Round(absoluteQuantity * stock.CostPrice, 2),
            MrpValue = Math.Round(absoluteQuantity * stock.MRP, 2),
            OutMovementId = outMovementId,
            InMovementId = inMovementId,
            Reason = reason,
            CompanyId = stock.CompanyId
        };
    }

    private static StockMovement CreateMovement(
        Stock stock,
        string movementType,
        decimal quantityIn,
        decimal quantityOut,
        StockOperationDocument document,
        string remarks)
    {
        var movement = new StockMovement
        {
            StockId = stock.Id,
            ProductId = stock.ProductId,
            Barcode = stock.Barcode,
            MovementType = movementType,
            QuantityIn = quantityIn,
            QuantityOut = quantityOut,
            CostPrice = stock.CostPrice,
            MRP = stock.MRP,
            TaxRate = stock.TaxRate,
            HSNCode = stock.HSNCode ?? stock.Product?.HSNCode,
            SourceType = "StockOperationDocument",
            SourceId = document.Id,
            SourceNumber = document.DocumentNumber,
            Remarks = remarks,
            OnDate = document.OnDate,
            CompanyId = stock.CompanyId,
            StoreGroupId = stock.StoreGroupId,
            StoreId = stock.StoreId
        };
        return movement;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ApplyAccountingResult(
        StockOperationDocument document,
        StockOperationAccountingResult? result)
    {
        document.JournalEntryId = result?.JournalEntryId;
        document.AccountingStatus = result is null ? "Not Required" : "Posted";
    }
}
