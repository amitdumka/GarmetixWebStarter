using Garmetix.Api.Inventory;
using Garmetix.Api.ProductLookup;
using Xunit;

namespace Garmetix.Api.Tests.Inventory;

public sealed class StockOperationCalculatorTests
{
    [Fact]
    public void Adjustment_IncreaseAndDecrease_PreserveQuantityEquation()
    {
        var increase = StockOperationCalculator.Adjustment(10, 4, true);
        var decrease = StockOperationCalculator.Adjustment(increase.AfterQuantity, 3, false);

        Assert.Equal(14, increase.AfterQuantity);
        Assert.Equal(4, increase.QuantityIn);
        Assert.Equal(11, decrease.AfterQuantity);
        Assert.Equal(3, decrease.QuantityOut);
        Assert.Equal(-3, decrease.Difference);
    }

    [Fact]
    public void Adjustment_RejectsReductionBeyondAvailableStock()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => StockOperationCalculator.Adjustment(2, 3, false));

        Assert.Contains("below zero", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(10, 13, 3, 3, 0)]
    [InlineData(10, 7, -3, 0, 3)]
    [InlineData(10, 10, 0, 0, 0)]
    public void PhysicalCount_CalculatesGainLossAndVerification(
        decimal current,
        decimal counted,
        decimal difference,
        decimal quantityIn,
        decimal quantityOut)
    {
        var result = StockOperationCalculator.PhysicalCount(current, counted);

        Assert.Equal(counted, result.AfterQuantity);
        Assert.Equal(difference, result.Difference);
        Assert.Equal(quantityIn, result.QuantityIn);
        Assert.Equal(quantityOut, result.QuantityOut);
    }

    [Fact]
    public void Transfer_RejectsQuantityBeyondAvailableStock()
    {
        Assert.Throws<ArgumentException>(() => StockOperationCalculator.Transfer(5, 6));
    }

    [Fact]
    public void StockOperationQrToken_RoundTrips()
    {
        var id = Guid.NewGuid();
        var token = DocumentCodeService.Create(DocumentCodeService.StockOperation, id);

        Assert.True(DocumentCodeService.TryParse(token, out var documentType, out var parsedId));
        Assert.Equal(DocumentCodeService.StockOperation, documentType);
        Assert.Equal(id, parsedId);
    }
}
