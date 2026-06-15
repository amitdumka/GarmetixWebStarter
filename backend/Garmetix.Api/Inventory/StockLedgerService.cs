using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Inventory;

public sealed class StockLedgerService(GarmetixDbContext db)
{
    public async Task<IReadOnlyDictionary<Guid, StockLedgerSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<Stock> stocks,
        CancellationToken cancellationToken)
    {
        if (stocks.Count == 0)
        {
            return new Dictionary<Guid, StockLedgerSnapshot>();
        }

        var stockIds = stocks.Select(item => item.Id).ToArray();
        var movementRows = await db.StockMovements.AsNoTracking()
            .Where(item => item.StockId.HasValue && stockIds.Contains(item.StockId.Value))
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new
            {
                StockId = item.StockId!.Value,
                Movement = new StockLedgerMovement(
                    item.Id,
                    item.OnDate,
                    item.CreatedAt,
                    item.QuantityIn,
                    item.QuantityOut,
                    item.CostPrice)
            })
            .ToListAsync(cancellationToken);

        var grouped = movementRows
            .GroupBy(item => item.StockId)
            .ToDictionary(
                group => group.Key,
                group => StockLedgerCalculator.Replay(group.Select(item => item.Movement)));

        return stocks.ToDictionary(
            stock => stock.Id,
            stock => grouped.GetValueOrDefault(stock.Id, StockLedgerSnapshot.Empty));
    }

    public async Task<StockLedgerSnapshot> GetSnapshotAsync(Stock stock, CancellationToken cancellationToken)
    {
        var movements = await db.StockMovements.AsNoTracking()
            .Where(item => item.StockId == stock.Id)
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => new StockLedgerMovement(
                item.Id,
                item.OnDate,
                item.CreatedAt,
                item.QuantityIn,
                item.QuantityOut,
                item.CostPrice))
            .ToListAsync(cancellationToken);

        movements.AddRange(db.ChangeTracker.Entries<StockMovement>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.StockId == stock.Id)
            .Select(entry => new StockLedgerMovement(
                entry.Entity.Id,
                entry.Entity.OnDate,
                entry.Entity.CreatedAt,
                entry.Entity.QuantityIn,
                entry.Entity.QuantityOut,
                entry.Entity.CostPrice)));

        return StockLedgerCalculator.Replay(movements);
    }

    public async Task<StockLedgerPosting> PostAsync(
        Stock stock,
        StockMovement movement,
        CancellationToken cancellationToken,
        bool allowNegative = false)
    {
        if (stock.IsOFB)
        {
            throw new InvalidOperationException("Regular stock ledger posting cannot be used for Off Book stock.");
        }

        movement.StockId = stock.Id;
        movement.ProductId = stock.ProductId;
        movement.Barcode = stock.Barcode;
        movement.CompanyId = stock.CompanyId;
        movement.StoreGroupId = stock.StoreGroupId;
        movement.StoreId = stock.StoreId;

        var movements = await db.StockMovements
            .Where(item => item.StockId == stock.Id)
            .ToListAsync(cancellationToken);
        movements.AddRange(db.ChangeTracker.Entries<StockMovement>()
            .Where(entry =>
                entry.State == EntityState.Added &&
                entry.Entity.StockId == stock.Id &&
                !ReferenceEquals(entry.Entity, movement))
            .Select(entry => entry.Entity));
        movements.Add(movement);

        var snapshot = StockLedgerSnapshot.Empty;
        StockLedgerPosting? requestedPosting = null;
        foreach (var item in movements
            .OrderBy(item => item.OnDate)
            .ThenBy(item => item.CreatedAt)
            .ThenBy(item => item.Id))
        {
            var incomingCost = item.QuantityIn > 0 && item.CostPrice <= 0 && snapshot.AverageCost > 0
                ? snapshot.AverageCost
                : item.CostPrice;
            var isRequestedMovement = ReferenceEquals(item, movement);
            var posting = StockLedgerCalculator.Apply(
                snapshot,
                item.QuantityIn,
                item.QuantityOut,
                incomingCost,
                item.OnDate,
                allowNegative: !isRequestedMovement || allowNegative);

            ApplyPosting(item, posting);
            snapshot = posting.After;
            if (isRequestedMovement)
            {
                requestedPosting = posting;
            }
        }

        ApplyProjection(stock, snapshot);
        db.StockMovements.Add(movement);
        return requestedPosting ?? throw new InvalidOperationException("Stock ledger posting could not be calculated.");
    }

    public async Task<StockLedgerSnapshot> RebuildProjectionAsync(Stock stock, CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(stock, cancellationToken);
        ApplyProjection(stock, snapshot);
        return snapshot;
    }

    private static void ApplyProjection(Stock stock, StockLedgerSnapshot snapshot)
    {
        stock.PurchaseQty = snapshot.TotalQuantityIn;
        stock.SoldQty = snapshot.TotalQuantityOut;
        stock.CostPrice = snapshot.AverageCost;
    }

    private static void ApplyPosting(StockMovement movement, StockLedgerPosting posting)
    {
        movement.CostPrice = posting.EffectiveUnitCost;
        movement.QuantityBefore = posting.Before.Quantity;
        movement.QuantityAfter = posting.After.Quantity;
        movement.AverageCostBefore = posting.Before.AverageCost;
        movement.AverageCostAfter = posting.After.AverageCost;
        movement.InventoryValueBefore = posting.Before.InventoryValue;
        movement.InventoryValueAfter = posting.After.InventoryValue;
        movement.CostImpact = posting.CostImpact;
        movement.ValuationMethod = "WeightedAverage";
    }
}
