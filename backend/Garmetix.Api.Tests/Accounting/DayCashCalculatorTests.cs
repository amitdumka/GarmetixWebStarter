using Garmetix.Api.Accounting;
using Xunit;

namespace Garmetix.Api.Tests.Accounting;

public class DayCashCalculatorTests
{
    [Fact]
    public void CashInHand_adds_money_in_and_subtracts_money_out_including_salary()
    {
        var movements = new DayCashMovements(
            Sales: 10000m, Receipts: 500m, DueReceipts: 300m, BankWithdrawal: 1000m,
            Expenses: 200m, Payments: 150m, CustomerDue: 700m, BankDeposit: 2000m,
            NonCashSale: 3000m, SalaryPayments: 1200m, CashRefunds: 50m);

        // Cash refunds are already inside Payments, so they must not be subtracted twice.
        Assert.Equal(1000m + 11800m - 7250m, movements.CashInHand(1000m));
    }

    [Fact]
    public void Notes_are_only_added_for_salary_and_refunds_that_happened()
    {
        var none = new DayCashMovements(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var both = none with { SalaryPayments = 10m, CashRefunds = 5m };

        Assert.Empty(none.Notes());
        Assert.Equal(2, both.Notes().Count());
    }
}
