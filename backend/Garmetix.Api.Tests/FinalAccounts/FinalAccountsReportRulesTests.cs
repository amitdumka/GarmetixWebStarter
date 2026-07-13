using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsReportRulesTests
{
    [Fact]
    public void OpeningSignedBalanceHonorsNaturalBalance()
    {
        Assert.Equal(125m, FinalAccountsReportRules.OpeningSignedBalance(125m, FinalAccountsNaturalBalance.Debit));
        Assert.Equal(-125m, FinalAccountsReportRules.OpeningSignedBalance(125m, FinalAccountsNaturalBalance.Credit));
    }

    [Fact]
    public void SplitSignedBalanceReturnsDebitOrCreditColumn()
    {
        Assert.Equal((100m, 0m), FinalAccountsReportRules.SplitSignedBalance(100m));
        Assert.Equal((0m, 75m), FinalAccountsReportRules.SplitSignedBalance(-75m));
        Assert.Equal((0m, 0m), FinalAccountsReportRules.SplitSignedBalance(0m));
    }

    [Fact]
    public void BalanceStatusAllowsTinyRoundingNoise()
    {
        Assert.Equal("Balanced", FinalAccountsReportRules.BalanceStatus(0.004m));
        Assert.Equal("Difference", FinalAccountsReportRules.BalanceStatus(1m));
    }

    [Fact]
    public void PeriodKeySupportsMonthlyAndQuarterlyComparison()
    {
        var date = new DateTime(2026, 7, 13);

        Assert.Equal("2026-07", FinalAccountsReportRules.PeriodKey(date, "Monthly"));
        Assert.Equal("2026-Q3", FinalAccountsReportRules.PeriodKey(date, "Quarterly"));
    }

    [Fact]
    public void PageSizeIsBoundedForLedgerReports()
    {
        Assert.Equal(1, FinalAccountsReportRules.NormalizePage(0));
        Assert.Equal(FinalAccountsReportRules.DefaultPageSize, FinalAccountsReportRules.NormalizePageSize(null));
        Assert.Equal(FinalAccountsReportRules.MaxPageSize, FinalAccountsReportRules.NormalizePageSize(9999));
    }
}
