using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
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
