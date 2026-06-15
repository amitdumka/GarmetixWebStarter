using Garmetix.Api.Accounting;
using Xunit;

namespace Garmetix.Api.Tests.Accounting;

public sealed class StockOperationAccountingCalculatorTests
{
    [Theory]
    [InlineData(StockOperationAccountingKind.Excess, 125.45, 125.45, 0, 0, 125.45, 0)]
    [InlineData(StockOperationAccountingKind.Shortage, 125.45, 0, 125.45, 0, 0, 125.45)]
    [InlineData(StockOperationAccountingKind.WriteOff, 125.45, 0, 125.45, 0, 0, 125.45)]
    [InlineData(StockOperationAccountingKind.Transfer, 125.45, 0, 125.45, 125.45, 0, 0)]
    public void Create_ReturnsBalancedOperationPlan(
        StockOperationAccountingKind kind,
        decimal amount,
        decimal sourceDebit,
        decimal sourceCredit,
        decimal destinationDebit,
        decimal gainCredit,
        decimal lossDebit)
    {
        var plan = StockOperationAccountingCalculator.Create(kind, amount);

        Assert.Equal(sourceDebit, plan.SourceInventoryDebit);
        Assert.Equal(sourceCredit, plan.SourceInventoryCredit);
        Assert.Equal(destinationDebit, plan.DestinationInventoryDebit);
        Assert.Equal(gainCredit, plan.GainCredit);
        Assert.Equal(lossDebit, plan.LossDebit);
        Assert.Equal(
            plan.SourceInventoryDebit + plan.DestinationInventoryDebit + plan.LossDebit,
            plan.SourceInventoryCredit + plan.GainCredit);
    }

    [Fact]
    public void Create_UsesAbsoluteAmountAndRoundsToPaise()
    {
        var plan = StockOperationAccountingCalculator.Create(StockOperationAccountingKind.WriteOff, -10.125m);

        Assert.Equal(10.13m, plan.SourceInventoryCredit);
        Assert.Equal(10.13m, plan.LossDebit);
    }

    [Fact]
    public void Create_ZeroValueRequiresNoJournalLines()
    {
        var plan = StockOperationAccountingCalculator.Create(StockOperationAccountingKind.Excess, 0);

        Assert.Equal(0, plan.SourceInventoryDebit);
        Assert.Equal(0, plan.GainCredit);
    }
}
