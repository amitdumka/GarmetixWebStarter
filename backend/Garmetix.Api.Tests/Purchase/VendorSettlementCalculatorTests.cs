using Garmetix.Api.Purchase;
using Xunit;

namespace Garmetix.Api.Tests.Purchase;

public sealed class VendorSettlementCalculatorTests
{
    [Fact]
    public void BuildsMixedSettlementWithinAvailableDebitNote()
    {
        var plan = VendorSettlementCalculator.Build(
            10_000,
            1_000,
            2_000,
            [
                new VendorSettlementAllocationInput(Guid.NewGuid(), 3_000, 4_000),
                new VendorSettlementAllocationInput(Guid.NewGuid(), 1_500, 1_500)
            ]);

        Assert.Equal(9_000, plan.AvailableAmount);
        Assert.Equal(4_500, plan.AdjustedAmount);
        Assert.Equal(2_000, plan.RefundAmount);
        Assert.Equal(6_500, plan.TotalAmount);
        Assert.Equal("Mixed", plan.SettlementType);
    }

    [Fact]
    public void RejectsSettlementAboveAvailableDebitNote()
    {
        var exception = Assert.Throws<ArgumentException>(() => VendorSettlementCalculator.Build(
            5_000,
            2_000,
            1_500,
            [new VendorSettlementAllocationInput(Guid.NewGuid(), 2_000, 3_000)]));

        Assert.Contains("available debit-note amount", exception.Message);
    }

    [Fact]
    public void RejectsAllocationAboveInvoiceOutstanding()
    {
        var exception = Assert.Throws<ArgumentException>(() => VendorSettlementCalculator.Build(
            5_000,
            0,
            0,
            [new VendorSettlementAllocationInput(Guid.NewGuid(), 2_001, 2_000)]));

        Assert.Contains("outstanding amount", exception.Message);
    }
}
