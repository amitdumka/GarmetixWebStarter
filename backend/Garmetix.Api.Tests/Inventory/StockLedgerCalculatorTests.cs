using Garmetix.Api.Inventory;
using Xunit;

namespace Garmetix.Api.Tests.Inventory;

public sealed class StockLedgerCalculatorTests
{
    [Fact]
    public void Apply_RecalculatesWeightedAverageForIncomingStock()
    {
        var opening = StockLedgerCalculator.Apply(
            StockLedgerSnapshot.Empty, 10, 0, 100, new DateTime(2026, 6, 1));
        var purchase = StockLedgerCalculator.Apply(
            opening.After, 10, 0, 120, new DateTime(2026, 6, 2));

        Assert.Equal(20, purchase.After.Quantity);
        Assert.Equal(110, purchase.After.AverageCost);
        Assert.Equal(2200, purchase.After.InventoryValue);
    }

    [Fact]
    public void Apply_ConsumesOutgoingStockAtCurrentAverageCost()
    {
        var before = new StockLedgerSnapshot(20, 0, 20, 110, 2200, new DateTime(2026, 6, 2));

        var sale = StockLedgerCalculator.Apply(before, 0, 5, 0, new DateTime(2026, 6, 3));

        Assert.Equal(15, sale.After.Quantity);
        Assert.Equal(110, sale.After.AverageCost);
        Assert.Equal(1650, sale.After.InventoryValue);
        Assert.Equal(-550, sale.CostImpact);
    }

    [Fact]
    public void Apply_RecalculatesAverageAfterSaleAndNewPurchase()
    {
        var before = new StockLedgerSnapshot(20, 5, 15, 110, 1650, new DateTime(2026, 6, 3));

        var purchase = StockLedgerCalculator.Apply(before, 5, 0, 140, new DateTime(2026, 6, 4));

        Assert.Equal(20, purchase.After.Quantity);
        Assert.Equal(117.5m, purchase.After.AverageCost);
        Assert.Equal(2350, purchase.After.InventoryValue);
    }

    [Fact]
    public void Apply_RejectsNegativeStock()
    {
        var before = new StockLedgerSnapshot(3, 0, 3, 50, 150, new DateTime(2026, 6, 1));

        var exception = Assert.Throws<ArgumentException>(() =>
            StockLedgerCalculator.Apply(before, 0, 4, 0, new DateTime(2026, 6, 2)));

        Assert.Contains("Insufficient ledger stock", exception.Message);
    }

    [Fact]
    public void Apply_AllowsExactAvailableQuantityToBeConsumed()
    {
        var before = new StockLedgerSnapshot(3, 0, 3, 50, 150, new DateTime(2026, 6, 1));

        var posting = StockLedgerCalculator.Apply(before, 0, 3, 0, new DateTime(2026, 6, 2));

        Assert.Equal(0, posting.After.Quantity);
        Assert.Equal(0, posting.After.InventoryValue);
        Assert.Equal(0, posting.After.AverageCost);
    }

    [Fact]
    public void Apply_RejectsNewOutflowWhenHistoricalBalanceIsAlreadyNegative()
    {
        var before = new StockLedgerSnapshot(2, 3, -1, 0, -50, new DateTime(2026, 6, 1));

        var exception = Assert.Throws<ArgumentException>(() =>
            StockLedgerCalculator.Apply(before, 0, 1, 0, new DateTime(2026, 6, 2)));

        Assert.Contains("Available quantity is -1", exception.Message);
    }

    [Fact]
    public void Apply_AllowsIncomingCorrectionOfHistoricalNegativeBalance()
    {
        var before = new StockLedgerSnapshot(2, 3, -1, 0, -50, new DateTime(2026, 6, 1));

        var posting = StockLedgerCalculator.Apply(before, 0.5m, 0, 100, new DateTime(2026, 6, 2));

        Assert.Equal(-0.5m, posting.After.Quantity);
        Assert.Equal(0, posting.After.AverageCost);
    }

    [Fact]
    public void Apply_RejectsMovementWithBothDirections()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            StockLedgerCalculator.Apply(StockLedgerSnapshot.Empty, 1, 1, 10, new DateTime(2026, 6, 1)));

        Assert.Contains("both inward and outward", exception.Message);
    }

    [Fact]
    public void Replay_UsesMovementDateAndCreationOrder()
    {
        var movements = new[]
        {
            new StockLedgerMovement(Guid.NewGuid(), new DateTime(2026, 6, 3), new DateTime(2026, 6, 3, 10, 0, 0), 0, 5, 0),
            new StockLedgerMovement(Guid.NewGuid(), new DateTime(2026, 6, 1), new DateTime(2026, 6, 1, 10, 0, 0), 10, 0, 100),
            new StockLedgerMovement(Guid.NewGuid(), new DateTime(2026, 6, 2), new DateTime(2026, 6, 2, 10, 0, 0), 10, 0, 120)
        };

        var snapshot = StockLedgerCalculator.Replay(movements);

        Assert.Equal(15, snapshot.Quantity);
        Assert.Equal(110, snapshot.AverageCost);
        Assert.Equal(1650, snapshot.InventoryValue);
    }

    [Fact]
    public void Replay_PreservesLegacyNegativeHistoryForRepair()
    {
        var movements = new[]
        {
            new StockLedgerMovement(Guid.NewGuid(), new DateTime(2026, 6, 1), new DateTime(2026, 6, 1, 10, 0, 0), 2, 0, 50),
            new StockLedgerMovement(Guid.NewGuid(), new DateTime(2026, 6, 2), new DateTime(2026, 6, 2, 10, 0, 0), 0, 3, 0)
        };

        var snapshot = StockLedgerCalculator.Replay(movements);

        Assert.Equal(-1, snapshot.Quantity);
        Assert.Equal(3, snapshot.TotalQuantityOut);
    }
}
