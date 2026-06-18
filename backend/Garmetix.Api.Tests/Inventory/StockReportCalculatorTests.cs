using Garmetix.Api.Inventory;
using Xunit;

namespace Garmetix.Api.Tests.Inventory;

public sealed class StockReportCalculatorTests
{
    private static readonly DateTime AsOf = new(2026, 6, 15);

    [Theory]
    [InlineData(0, "0-30 Days")]
    [InlineData(30, "0-30 Days")]
    [InlineData(31, "31-60 Days")]
    [InlineData(60, "31-60 Days")]
    [InlineData(61, "61-90 Days")]
    [InlineData(90, "61-90 Days")]
    [InlineData(91, "91-180 Days")]
    [InlineData(180, "91-180 Days")]
    [InlineData(181, "180+ Days")]
    public void AgeBucket_UsesFixedReceiptAgeBoundaries(int days, string expected)
    {
        var result = StockReportCalculator.AgeBucket(1, AsOf.AddDays(-days), AsOf);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AgeBucket_IdentifiesStockWithoutReceiptHistory()
    {
        Assert.Equal("No Receipt History", StockReportCalculator.AgeBucket(1, null, AsOf));
    }

    [Fact]
    public void AgeBucket_PrioritizesOutOfStock()
    {
        Assert.Equal("Out of Stock", StockReportCalculator.AgeBucket(0, AsOf.AddDays(-200), AsOf));
    }

    [Theory]
    [InlineData(0, 3, "Critical")]
    [InlineData(-1, 3, "Critical")]
    [InlineData(1, 3, "Low")]
    [InlineData(3, 3, "Low")]
    [InlineData(4, 3, "Watch")]
    [InlineData(6, 3, "Watch")]
    [InlineData(7, 3, "Healthy")]
    public void Risk_UsesConfiguredLowStockThreshold(decimal quantity, decimal threshold, string expected)
    {
        Assert.Equal(expected, StockReportCalculator.Risk(quantity, threshold));
    }

    [Fact]
    public void Reconciliation_AllowsMinorRoundingDifferences()
    {
        var result = StockReportCalculator.Reconciliation(10, 10.009m, 125, 125.009m);

        Assert.Equal("Matched", result);
    }

    [Theory]
    [InlineData(10, 10.02, 125, 125)]
    [InlineData(10, 10, 125, 125.02)]
    public void Reconciliation_IdentifiesQuantityOrCostMismatch(
        decimal ledgerQuantity,
        decimal projectedQuantity,
        decimal ledgerAverageCost,
        decimal projectedAverageCost)
    {
        var result = StockReportCalculator.Reconciliation(
            ledgerQuantity,
            projectedQuantity,
            ledgerAverageCost,
            projectedAverageCost);

        Assert.Equal("Mismatch", result);
    }
}
