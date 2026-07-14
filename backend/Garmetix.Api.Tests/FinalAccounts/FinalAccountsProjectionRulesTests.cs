using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsProjectionRulesTests
{
    [Fact]
    public void ProjectionBuildsRequestedMonthlyHorizon()
    {
        var months = FinalAccountsProjectionRules.BuildProjection(Baseline(), Assumptions(), new DateTime(2026, 4, 1), 12);

        Assert.Equal(12, months.Count);
        Assert.Equal(new DateTime(2026, 4, 1), months[0].MonthStart);
        Assert.Equal(new DateTime(2027, 3, 1), months[^1].MonthStart);
        Assert.All(months, item => Assert.True(item.Revenue > 0m));
    }

    [Fact]
    public void ProjectionProducesStatementsCashFlowAndRatios()
    {
        var months = FinalAccountsProjectionRules.BuildProjection(Baseline(), Assumptions(), new DateTime(2026, 4, 1), 12);
        var summary = FinalAccountsProjectionRules.Summarize(months);

        Assert.True(summary.TotalRevenue > 0m);
        Assert.True(months[0].BreakEvenRevenue > 0m);
        Assert.True(months[0].WorkingCapitalRequirement > 0m);
        Assert.NotEqual(0m, months[0].OperatingCashFlow);
        Assert.Equal(months[^1].CashBalance, summary.ClosingCash);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(61)]
    public void HorizonMustBeOneToFiveYears(int horizonMonths)
    {
        Assert.Throws<ArgumentException>(() => FinalAccountsProjectionRules.NormalizeHorizon(horizonMonths));
    }

    [Fact]
    public void SeasonalityRequiresTwelveFactors()
    {
        Assert.Throws<ArgumentException>(() => FinalAccountsProjectionRules.NormalizeSeasonality(new[] { 1m, 1m }));
    }

    [Theory]
    [InlineData(FinalAccountsProjectionScenarioStatus.Draft, true, false)]
    [InlineData(FinalAccountsProjectionScenarioStatus.Submitted, false, true)]
    [InlineData(FinalAccountsProjectionScenarioStatus.Approved, false, false)]
    public void WorkflowGatesScenarioApproval(
        FinalAccountsProjectionScenarioStatus status,
        bool canSubmit,
        bool canApprove)
    {
        Assert.Equal(canSubmit, FinalAccountsProjectionRules.CanSubmit(status));
        Assert.Equal(canApprove, FinalAccountsProjectionRules.CanApprove(status));
    }

    private static FinalAccountsProjectionBaselineDto Baseline()
        => new(100000m, 42000m, 12000m, 250000m, 180000m, 90000m, 75000m, 600000m, 200000m, 845000m);

    private static FinalAccountsProjectionAssumptionDto Assumptions()
        => new(
            12m,
            Enumerable.Repeat(1m, 12).ToList(),
            10000m,
            1800m,
            5m,
            3m,
            42m,
            4m,
            45m,
            28m,
            35m,
            18000m,
            8m,
            15000m,
            6m,
            10000m,
            10m,
            200000m,
            12m,
            5000m,
            4000m,
            3000m,
            25m,
            50000m,
            "Base projection");
}
