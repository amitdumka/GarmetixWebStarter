using Garmetix.Api.Auth;
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
        group.MapPost("/adjustment", CreateAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/transfer", CreateTransferAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/physical-count", CreatePhysicalCountAsync).RequireAuthorization(GarmetixPolicies.Edit);

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

    private static IQueryable<StockMovementRowDto> MovementQuery(HttpContext context, GarmetixDbContext db, int take)
    {
        // Keep ordering on entity fields before projecting into StockMovementRowDto.
        // EF Core cannot translate OrderBy(new StockMovementRowDto(...).OnDate).
        return WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
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
                item.SourceNumber,
                item.Remarks));
    }

    private static async Task<IResult> CreateAdjustmentAsync(
        StockAdjustmentRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
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

        if (!increase && stock.CurrentStock < request.Quantity)
        {
            return Results.BadRequest(new { message = $"Cannot reduce {request.Quantity:0.##}. Available stock is {stock.CurrentStock:0.##}." });
        }

        var operationNumber = await documentNumbers.NextStockAdjustmentAsync(stock.CompanyId, stock.StoreGroupId, stock.StoreId, cancellationToken);
        var quantityIn = increase ? request.Quantity : 0;
        var quantityOut = increase ? 0 : request.Quantity;

        if (increase)
        {
            stock.PurchaseQty += request.Quantity;
        }
        else
        {
            stock.SoldQty += request.Quantity;
        }

        AddMovement(db, stock, increase ? "StockAdjustmentIn" : "StockAdjustmentOut", quantityIn, quantityOut, "StockAdjustment", stock.Id, operationNumber, Clean(request.Reason) ?? "Manual stock adjustment");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockOperationResponse(
            operationNumber,
            stock.ProductId,
            stock.Barcode,
            stock.StoreId,
            quantityIn,
            quantityOut,
            stock.CurrentStock,
            increase ? "StockAdjustmentIn" : "StockAdjustmentOut",
            "Stock adjustment posted."));
    }

    private static async Task<IResult> CreatePhysicalCountAsync(
        PhysicalStockCountRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
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

        var current = stock.CurrentStock;
        var difference = request.CountedQuantity - current;
        if (difference == 0)
        {
            return Results.Ok(new StockOperationResponse(
                "NO-CHANGE",
                stock.ProductId,
                stock.Barcode,
                stock.StoreId,
                0,
                0,
                stock.CurrentStock,
                "PhysicalCountNoChange",
                "Physical count matched system stock. No movement posted."));
        }

        var operationNumber = await documentNumbers.NextPhysicalStockCountAsync(stock.CompanyId, stock.StoreGroupId, stock.StoreId, cancellationToken);
        var quantityIn = difference > 0 ? difference : 0;
        var quantityOut = difference < 0 ? Math.Abs(difference) : 0;

        if (difference > 0)
        {
            stock.PurchaseQty += difference;
        }
        else
        {
            stock.SoldQty += Math.Abs(difference);
        }

        AddMovement(db, stock, difference > 0 ? "PhysicalCountGain" : "PhysicalCountLoss", quantityIn, quantityOut, "PhysicalCount", stock.Id, operationNumber, Clean(request.Reason) ?? $"Physical count set to {request.CountedQuantity:0.##}");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockOperationResponse(
            operationNumber,
            stock.ProductId,
            stock.Barcode,
            stock.StoreId,
            quantityIn,
            quantityOut,
            stock.CurrentStock,
            difference > 0 ? "PhysicalCountGain" : "PhysicalCountLoss",
            "Physical stock count posted."));
    }

    private static async Task<IResult> CreateTransferAsync(
        StockTransferRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
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

        if (fromStock.CurrentStock < request.Quantity)
        {
            return Results.BadRequest(new { message = $"Cannot transfer {request.Quantity:0.##}. Available stock is {fromStock.CurrentStock:0.##}." });
        }

        await DocumentNumberGenerator.LockStockKeyAsync(db, fromStock.CompanyId, fromStock.StoreGroupId, fromStock.StoreId, fromStock.ProductId, fromStock.Barcode, cancellationToken);
        await DocumentNumberGenerator.LockStockKeyAsync(db, fromStock.CompanyId, toStore.StoreGroupId, toStore.Id, fromStock.ProductId, fromStock.Barcode, cancellationToken);

        var toStock = await db.Stocks.FirstOrDefaultAsync(item =>
            item.CompanyId == fromStock.CompanyId &&
            item.StoreGroupId == toStore.StoreGroupId &&
            item.StoreId == toStore.Id &&
            item.ProductId == fromStock.ProductId &&
            item.Barcode == fromStock.Barcode,
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
                CompanyId = fromStock.CompanyId,
                StoreGroupId = toStore.StoreGroupId,
                StoreId = toStore.Id
            };
            db.Stocks.Add(toStock);
        }

        var operationNumber = await documentNumbers.NextStockTransferAsync(fromStock.CompanyId, fromStock.StoreGroupId, fromStock.StoreId, cancellationToken);
        var reason = Clean(request.Reason) ?? $"Transfer to {toStore.Name}";

        fromStock.SoldQty += request.Quantity;
        toStock.PurchaseQty += request.Quantity;

        AddMovement(db, fromStock, "StockTransferOut", 0, request.Quantity, "StockTransfer", toStock.Id, operationNumber, reason);
        AddMovement(db, toStock, "StockTransferIn", request.Quantity, 0, "StockTransfer", fromStock.Id, operationNumber, $"Transfer from store {fromStock.StoreId}");

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new StockTransferResponse(
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
            .FirstOrDefaultAsync(item => item.Id == stockId, cancellationToken);
    }

    private static void AddMovement(
        GarmetixDbContext db,
        Stock stock,
        string movementType,
        decimal quantityIn,
        decimal quantityOut,
        string sourceType,
        Guid? sourceId,
        string sourceNumber,
        string remarks)
    {
        db.StockMovements.Add(new StockMovement
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
            SourceType = sourceType,
            SourceId = sourceId,
            SourceNumber = sourceNumber,
            Remarks = remarks,
            OnDate = DateTime.Now,
            CompanyId = stock.CompanyId,
            StoreGroupId = stock.StoreGroupId,
            StoreId = stock.StoreId
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
