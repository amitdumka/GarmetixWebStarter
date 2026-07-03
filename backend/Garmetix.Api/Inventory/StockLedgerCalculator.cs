namespace Garmetix.Api.Inventory;

public sealed record StockLedgerSnapshot(
    decimal TotalQuantityIn,
    decimal TotalQuantityOut,
    decimal Quantity,
    decimal AverageCost,
    decimal InventoryValue,
    DateTime? LastMovementAt)
{
    public static StockLedgerSnapshot Empty { get; } = new(0, 0, 0, 0, 0, null);
}

public sealed record StockLedgerPosting(
    StockLedgerSnapshot Before,
    StockLedgerSnapshot After,
    decimal EffectiveUnitCost,
    decimal CostImpact);

public static class StockLedgerCalculator
{
    public static StockLedgerPosting Apply(
        StockLedgerSnapshot before,
        decimal quantityIn,
        decimal quantityOut,
        decimal incomingUnitCost,
        DateTime onDate,
        bool allowNegative = false)
    {
        if (quantityIn < 0 || quantityOut < 0)
        {
            throw new ArgumentException("Stock movement quantities cannot be negative.");
        }

        if (quantityIn > 0 && quantityOut > 0)
        {
            throw new ArgumentException("A stock movement cannot contain both inward and outward quantity.");
        }

        if (quantityIn == 0 && quantityOut == 0)
        {
            throw new ArgumentException("A stock movement quantity is required.");
        }

        if (incomingUnitCost < 0)
        {
            throw new ArgumentException("Incoming stock cost cannot be negative.");
        }

        if (!allowNegative && quantityOut > 0 && quantityOut > before.Quantity)
        {
            throw new ArgumentException($"Insufficient ledger stock. Available quantity is {before.Quantity:0.##}.");
        }

        var effectiveUnitCost = quantityIn > 0 ? incomingUnitCost : before.AverageCost;
        var costImpact = quantityIn > 0
            ? RoundValue(quantityIn * effectiveUnitCost)
            : -RoundValue(quantityOut * effectiveUnitCost);
        var quantity = RoundQuantity(before.Quantity + quantityIn - quantityOut);
        var inventoryValue = RoundValue(before.InventoryValue + costImpact);

        if (quantity <= 0)
        {
            inventoryValue = quantity == 0 ? 0 : inventoryValue;
        }

        var averageCost = quantity > 0
            ? RoundCost(inventoryValue / quantity)
            : 0;
        var after = new StockLedgerSnapshot(
            RoundQuantity(before.TotalQuantityIn + quantityIn),
            RoundQuantity(before.TotalQuantityOut + quantityOut),
            quantity,
            averageCost,
            inventoryValue,
            onDate);

        return new StockLedgerPosting(before, after, effectiveUnitCost, costImpact);
    }

    public static StockLedgerSnapshot Replay(IEnumerable<StockLedgerMovement> movements)
    {
        var snapshot = StockLedgerSnapshot.Empty;
        foreach (var movement in movements.OrderBy(item => item.OnDate).ThenBy(item => item.CreatedAt).ThenBy(item => item.Id))
        {
            snapshot = Apply(
                snapshot,
                movement.QuantityIn,
                movement.QuantityOut,
                movement.CostPrice,
                movement.OnDate,
                allowNegative: true).After;
        }

        return snapshot;
    }

    private static decimal RoundQuantity(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundCost(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);
    private static decimal RoundValue(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record StockLedgerMovement(
    Guid Id,
    DateTime OnDate,
    DateTime CreatedAt,
    decimal QuantityIn,
    decimal QuantityOut,
    decimal CostPrice);
