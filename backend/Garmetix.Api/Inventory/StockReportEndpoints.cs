using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Inventory;

public static class StockReportEndpoints
{
    private static readonly string[] AgeBucketOrder =
    [
        "0-30 Days",
        "31-60 Days",
        "61-90 Days",
        "91-180 Days",
        "180+ Days",
        "No Receipt History",
        "Out of Stock"
    ];

    private static readonly string[] RiskBucketOrder = ["Critical", "Low", "Watch", "Healthy"];

    public static RouteGroupBuilder MapInventoryStockReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/stock-reports")
            .WithTags("Inventory Stock Reports")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("/summary", SummaryAsync);
        group.MapGet("/movement-history", MovementHistoryAsync);
        return group;
    }

    private static async Task<StockReportSummaryDto> SummaryAsync(
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        decimal? lowStockThreshold,
        CancellationToken cancellationToken)
    {
        var threshold = Math.Clamp(lowStockThreshold ?? 3m, 1m, 100000m);
        var asOf = DateTime.Today;
        var stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(stock => !stock.IsOFB)
            .Include(stock => stock.Product)
            .OrderBy(stock => stock.Product != null ? stock.Product.Name : stock.Barcode)
            .ThenBy(stock => stock.Barcode)
            .ToListAsync(cancellationToken);

        if (stocks.Count == 0)
        {
            return new(
                asOf,
                threshold,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                [],
                [],
                []);
        }

        var stockIds = stocks.Select(stock => stock.Id).ToArray();
        var snapshots = await stockLedger.GetSnapshotsAsync(stocks, cancellationToken);
        var movementStats = await db.StockMovements.AsNoTracking()
            .Where(movement => movement.StockId.HasValue && stockIds.Contains(movement.StockId.Value))
            .GroupBy(movement => movement.StockId!.Value)
            .Select(group => new
            {
                StockId = group.Key,
                LastMovementAt = group.Max(movement => (DateTime?)movement.OnDate),
                LastInwardAt = group
                    .Where(movement => movement.QuantityIn > 0)
                    .Max(movement => (DateTime?)movement.OnDate),
                MovementCount = group.Count()
            })
            .ToDictionaryAsync(row => row.StockId, cancellationToken);
        var storeNames = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);

        var rows = stocks.Select(stock =>
        {
            var snapshot = snapshots[stock.Id];
            movementStats.TryGetValue(stock.Id, out var movement);
            var projectedQuantity = stock.PurchaseQty - stock.SoldQty;
            var ageDays = movement?.LastInwardAt is null
                ? (int?)null
                : Math.Max(0, (asOf - movement.LastInwardAt.Value.Date).Days);
            return new StockReportRowDto(
                stock.Id,
                stock.ProductId,
                stock.Product?.Name ?? stock.Barcode,
                stock.Barcode,
                storeNames.GetValueOrDefault(stock.StoreId, "Store"),
                snapshot.Quantity,
                projectedQuantity,
                snapshot.AverageCost,
                stock.CostPrice,
                snapshot.InventoryValue,
                movement?.LastInwardAt,
                movement?.LastMovementAt,
                ageDays,
                StockReportCalculator.AgeBucket(snapshot.Quantity, movement?.LastInwardAt, asOf),
                StockReportCalculator.Risk(snapshot.Quantity, threshold),
                StockReportCalculator.Reconciliation(
                    snapshot.Quantity,
                    projectedQuantity,
                    snapshot.AverageCost,
                    stock.CostPrice),
                movement?.MovementCount ?? 0);
        })
        .OrderBy(row => Array.IndexOf(RiskBucketOrder, row.Risk))
        .ThenByDescending(row => row.AgeDays ?? -1)
        .ThenBy(row => row.ProductName)
        .ToList();

        var pendingAccountingDocuments = await WorkspaceScope.ApplyTo(
                db.StockOperationDocuments.AsNoTracking(),
                context)
            .CountAsync(document => document.AccountingStatus == "Pending", cancellationToken);

        return new StockReportSummaryDto(
            asOf,
            threshold,
            rows.Count,
            rows.Sum(row => row.LedgerQuantity),
            rows.Sum(row => row.InventoryValue),
            rows.Count(row => row.Risk is "Critical" or "Low"),
            rows.Count(row => row.AgeDays > 90 && row.LedgerQuantity > 0),
            rows.Count(row => row.ReconciliationStatus == "Mismatch"),
            pendingAccountingDocuments,
            Buckets(rows, AgeBucketOrder, row => row.AgeBucket),
            Buckets(rows, RiskBucketOrder, row => row.Risk),
            rows);
    }


    private static async Task<IResult> MovementHistoryAsync(
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        Guid? stockId,
        Guid? productId,
        string? barcode,
        int take = 500,
        CancellationToken cancellationToken = default)
    {
        var normalizedBarcode = barcode?.Trim();
        var limit = Math.Clamp(take, 1, 2000);

        var stocksQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(stock => !stock.IsOFB)
            .Include(stock => stock.Product)
            .AsQueryable();

        if (stockId.HasValue)
        {
            stocksQuery = stocksQuery.Where(stock => stock.Id == stockId.Value);
        }

        if (productId.HasValue)
        {
            stocksQuery = stocksQuery.Where(stock => stock.ProductId == productId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedBarcode))
        {
            stocksQuery = stocksQuery.Where(stock => stock.Barcode == normalizedBarcode);
        }

        var stocks = await stocksQuery
            .OrderBy(stock => stock.Product != null ? stock.Product.Name : stock.Barcode)
            .ThenBy(stock => stock.Barcode)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (stocks.Count == 0)
        {
            return Results.NotFound(new { message = "No matching stock row was found for the selected product, barcode or store." });
        }

        var stockIds = stocks.Select(stock => stock.Id).ToArray();
        var productIds = stocks.Select(stock => stock.ProductId).Distinct().ToArray();
        var barcodes = stocks.Select(stock => stock.Barcode).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();

        var movements = await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(movement =>
                (movement.StockId.HasValue && stockIds.Contains(movement.StockId.Value))
                || productIds.Contains(movement.ProductId)
                || barcodes.Contains(movement.Barcode))
            .OrderBy(movement => movement.OnDate)
            .ThenBy(movement => movement.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var storeIds = stocks.Select(stock => stock.StoreId).Concat(movements.Select(movement => movement.StoreId)).Distinct().ToArray();
        var storeNames = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(store => storeIds.Contains(store.Id))
            .ToDictionaryAsync(store => store.Id, store => store.Name, cancellationToken);

        var productNames = stocks
            .GroupBy(stock => stock.ProductId)
            .ToDictionary(group => group.Key, group => group.Select(stock => stock.Product?.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? group.First().Barcode);

        var saleSourceIds = movements
            .Where(movement => movement.SourceId.HasValue && (movement.SourceType == "SalesInvoice" || movement.SourceType == "SalesInvoiceImport" || movement.SourceType == "SalesExchange" || movement.SourceType == "SalesReturn"))
            .Select(movement => movement.SourceId!.Value)
            .Distinct()
            .ToArray();
        var saleLineLookup = await BuildInvoiceLineLookupAsync(db, saleSourceIds, cancellationToken);

        var purchaseSourceIds = movements
            .Where(movement => movement.SourceId.HasValue && (movement.SourceType == "PurchaseInvoice" || movement.SourceType == "PurchaseInvoiceImport"))
            .Select(movement => movement.SourceId!.Value)
            .Distinct()
            .ToArray();
        var purchaseLineLookup = await BuildPurchaseLineLookupAsync(db, purchaseSourceIds, cancellationToken);

        var snapshots = await stockLedger.GetSnapshotsAsync(stocks, cancellationToken);
        var totalCurrentQuantity = snapshots.Values.Sum(snapshot => snapshot.Quantity);
        var totalCurrentValue = snapshots.Values.Sum(snapshot => snapshot.InventoryValue);
        var weightedAverageCost = totalCurrentQuantity > 0 ? Math.Round(totalCurrentValue / totalCurrentQuantity, 4) : 0;
        var summaryProductName = stocks.Count == 1
            ? stocks[0].Product?.Name ?? stocks[0].Barcode
            : productNames.Values.FirstOrDefault() ?? "Multiple products";
        var summaryBarcode = stocks.Count == 1 ? stocks[0].Barcode : string.Join(", ", barcodes.Take(3));
        var summaryStoreName = stocks.Select(stock => stock.StoreId).Distinct().Count() == 1
            ? storeNames.GetValueOrDefault(stocks[0].StoreId, "Store")
            : "Multiple stores";

        var rows = movements.Select(movement =>
        {
            productNames.TryGetValue(movement.ProductId, out var productName);
            productName ??= stocks.FirstOrDefault(stock => stock.ProductId == movement.ProductId || stock.Id == movement.StockId)?.Product?.Name ?? movement.Barcode;
            var key = MovementLineKey(movement.SourceId, movement.ProductId, movement.Barcode);
            saleLineLookup.TryGetValue(key, out var saleLine);
            purchaseLineLookup.TryGetValue(key, out var purchaseLine);

            var quantityOut = Math.Max(movement.QuantityOut, 0);
            var quantityIn = Math.Max(movement.QuantityIn, 0);
            var signedQuantity = quantityIn - quantityOut;
            var isSale = IsSaleMovement(movement);
            var isSaleReturn = movement.SourceType == "SalesReturn" || movement.MovementType.Contains("ReturnIn", StringComparison.OrdinalIgnoreCase);
            var effectiveSaleAmount = isSale || isSaleReturn
                ? Math.Round((saleLine?.UnitAmount ?? movement.MRP) * (isSale ? quantityOut : quantityIn), 2)
                : (decimal?)null;
            var costQuantity = isSale ? quantityOut : isSaleReturn ? quantityIn : Math.Abs(signedQuantity);
            var costAmount = Math.Round(costQuantity * movement.CostPrice, 2);
            var profit = 0m;
            if (isSale && effectiveSaleAmount.HasValue)
            {
                profit = Math.Round(effectiveSaleAmount.Value - costAmount, 2);
            }
            else if (isSaleReturn && effectiveSaleAmount.HasValue)
            {
                profit = -Math.Round(effectiveSaleAmount.Value - costAmount, 2);
            }

            var purchaseUnitAmount = purchaseLine?.UnitAmount;
            var unitCost = purchaseUnitAmount.HasValue && movement.QuantityIn > 0 ? purchaseUnitAmount.Value : movement.CostPrice;

            return new StockMovementHistoryRowDto(
                movement.Id,
                movement.OnDate,
                productName,
                movement.Barcode,
                movement.MovementType,
                MovementLabel(movement),
                MovementDirection(movement),
                movement.SourceType,
                movement.SourceId,
                movement.SourceNumber,
                quantityIn,
                quantityOut,
                signedQuantity,
                unitCost,
                effectiveSaleAmount.HasValue && costQuantity > 0 ? Math.Round(effectiveSaleAmount.Value / costQuantity, 2) : null,
                costAmount,
                effectiveSaleAmount,
                profit,
                movement.QuantityBefore,
                movement.QuantityAfter,
                movement.AverageCostBefore,
                movement.AverageCostAfter,
                movement.InventoryValueBefore,
                movement.InventoryValueAfter,
                movement.Remarks);
        }).ToList();

        var summary = new StockMovementHistorySummaryDto(
            DateTime.Today,
            stocks.Count == 1 ? stocks[0].Id : null,
            stocks.Select(stock => stock.ProductId).Distinct().Count() == 1 ? stocks[0].ProductId : null,
            summaryProductName,
            summaryBarcode,
            summaryStoreName,
            totalCurrentQuantity,
            weightedAverageCost,
            totalCurrentValue,
            rows.Where(IsPurchaseInRow).Sum(row => row.QuantityIn),
            rows.Where(IsPurchaseReturnRow).Sum(row => row.QuantityOut),
            rows.Where(IsSaleOutRow).Sum(row => row.QuantityOut),
            rows.Where(IsSalesReturnRow).Sum(row => row.QuantityIn),
            rows.Sum(row => row.SalesAmount ?? 0),
            rows.Where(row => row.ProfitOrLoss != 0).Sum(row => row.CostAmount),
            rows.Sum(row => row.ProfitOrLoss),
            rows.Where(row => row.ProfitOrLoss > 0).Sum(row => row.ProfitOrLoss),
            rows.Where(row => row.ProfitOrLoss < 0).Sum(row => Math.Abs(row.ProfitOrLoss)),
            rows.OrderByDescending(row => row.OnDate).ToList());

        return Results.Ok(summary);
    }

    private sealed record MovementLineAmount(decimal Quantity, decimal Amount)
    {
        public decimal UnitAmount => Quantity > 0 ? Math.Round(Amount / Quantity, 4) : 0;
    }

    private static async Task<Dictionary<string, MovementLineAmount>> BuildInvoiceLineLookupAsync(
        GarmetixDbContext db,
        Guid[] invoiceIds,
        CancellationToken cancellationToken)
    {
        if (invoiceIds.Length == 0)
        {
            return new Dictionary<string, MovementLineAmount>();
        }

        var rows = await db.InvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => new { item.InvoiceId, item.ProductId, item.Barcode })
            .Select(group => new
            {
                group.Key.InvoiceId,
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(item => item.BilledQuantity),
                Amount = group.Sum(item => item.Amount)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => MovementLineKey(row.InvoiceId, row.ProductId, row.Barcode),
            row => new MovementLineAmount(row.Quantity, row.Amount));
    }

    private static async Task<Dictionary<string, MovementLineAmount>> BuildPurchaseLineLookupAsync(
        GarmetixDbContext db,
        Guid[] invoiceIds,
        CancellationToken cancellationToken)
    {
        if (invoiceIds.Length == 0)
        {
            return new Dictionary<string, MovementLineAmount>();
        }

        var rows = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => new { item.InvoiceId, item.ProductId, item.Barcode })
            .Select(group => new
            {
                group.Key.InvoiceId,
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(item => item.BilledQuantity),
                Amount = group.Sum(item => item.Amount)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => MovementLineKey(row.InvoiceId, row.ProductId, row.Barcode),
            row => new MovementLineAmount(row.Quantity, row.Amount));
    }

    private static string MovementLineKey(Guid? sourceId, Guid productId, string? barcode) => $"{sourceId:N}|{productId:N}|{(barcode ?? string.Empty).Trim()}";

    private static bool IsSaleMovement(StockMovement movement) =>
        movement.SourceType is "SalesInvoice" or "SalesInvoiceImport" or "SalesExchange" or "NonGstSale"
        || movement.MovementType.Contains("SaleOut", StringComparison.OrdinalIgnoreCase)
        || movement.MovementType.Contains("ExchangeSaleOut", StringComparison.OrdinalIgnoreCase);

    private static string MovementDirection(StockMovement movement)
    {
        if (movement.QuantityIn > 0 && movement.QuantityOut <= 0)
        {
            return "In";
        }

        if (movement.QuantityOut > 0 && movement.QuantityIn <= 0)
        {
            return "Out";
        }

        return "Adjustment";
    }

    private static string MovementLabel(StockMovement movement)
    {
        if (movement.SourceType == "PurchaseInvoice" || movement.MovementType.Contains("PurchaseIn", StringComparison.OrdinalIgnoreCase)) return "Purchase inward";
        if (movement.SourceType == "PurchaseReturn" || movement.MovementType.Contains("PurchaseReturn", StringComparison.OrdinalIgnoreCase)) return "Purchase return";
        if (movement.SourceType == "SalesInvoice" || movement.MovementType.Contains("SaleOut", StringComparison.OrdinalIgnoreCase)) return "Sale";
        if (movement.SourceType == "SalesReturn" || movement.MovementType.Contains("SalesReturn", StringComparison.OrdinalIgnoreCase)) return "Sale return";
        if (movement.MovementType.Contains("Opening", StringComparison.OrdinalIgnoreCase)) return "Opening stock";
        if (movement.SourceType == "StockOperationDocument") return "Stock operation";
        return string.IsNullOrWhiteSpace(movement.MovementType) ? "Movement" : movement.MovementType;
    }


    private static bool IsPurchaseInRow(StockMovementHistoryRowDto row) =>
        (row.SourceType is "PurchaseInvoice" or "PurchaseInvoiceImport" || row.MovementType.Contains("Purchase", StringComparison.OrdinalIgnoreCase))
        && row.QuantityIn > 0
        && !IsPurchaseReturnRow(row);

    private static bool IsPurchaseReturnRow(StockMovementHistoryRowDto row) =>
        row.SourceType == "PurchaseReturn" || row.MovementType.Contains("PurchaseReturn", StringComparison.OrdinalIgnoreCase);

    private static bool IsSaleOutRow(StockMovementHistoryRowDto row) =>
        (row.SourceType is "SalesInvoice" or "SalesInvoiceImport" or "SalesExchange" || row.MovementType.Contains("SaleOut", StringComparison.OrdinalIgnoreCase))
        && row.QuantityOut > 0;

    private static bool IsSalesReturnRow(StockMovementHistoryRowDto row) =>
        row.SourceType == "SalesReturn" || row.MovementType.Contains("SalesReturn", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<StockReportBucketDto> Buckets(
        IReadOnlyList<StockReportRowDto> rows,
        IEnumerable<string> order,
        Func<StockReportRowDto, string> selector)
    {
        return order.Select(label =>
        {
            var bucketRows = rows.Where(row => selector(row) == label).ToList();
            return new StockReportBucketDto(
                label,
                bucketRows.Count,
                bucketRows.Sum(row => row.LedgerQuantity),
                bucketRows.Sum(row => row.InventoryValue));
        }).ToList();
    }
}
