using Garmetix.Api.Purchase;
using Xunit;

namespace Garmetix.Api.Tests.Purchase;

public sealed class PurchaseReturnItcCalculatorTests
{
    [Fact]
    public void PartialReturnProratesTaxAndKeepsComponentsExact()
    {
        var result = PurchaseReturnItcCalculator.Calculate(
            new PurchaseReturnTaxSource(4, 1000, 120, 60, 60, 0, 40),
            1);

        Assert.Equal(250, result.TaxableAmount);
        Assert.Equal(30, result.TaxAmount);
        Assert.Equal(15, result.CgstAmount);
        Assert.Equal(15, result.SgstAmount);
        Assert.Equal(result.TaxAmount, result.CgstAmount + result.SgstAmount + result.IgstAmount);
        Assert.Equal(280, result.ReturnAmount);
    }

    [Fact]
    public void ComponentRoundingDifferenceIsAppliedToAComponent()
    {
        var result = PurchaseReturnItcCalculator.Calculate(
            new PurchaseReturnTaxSource(3, 10, 1, 0.50m, 0.50m, 0, 0),
            1);

        Assert.Equal(0.33m, result.TaxAmount);
        Assert.Equal(result.TaxAmount, result.CgstAmount + result.SgstAmount + result.IgstAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void InvalidReturnQuantityIsRejected(decimal returnedQuantity)
    {
        Assert.Throws<ArgumentException>(() => PurchaseReturnItcCalculator.Calculate(
            new PurchaseReturnTaxSource(4, 1000, 120, 60, 60, 0, 40),
            returnedQuantity));
    }
}
