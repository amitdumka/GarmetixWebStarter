using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsPeriodCloseRulesTests
{
    [Fact]
    public void RequiredBlockedChecklistPreventsClose()
    {
        var checklist = new[]
        {
            FinalAccountsPeriodCloseRules.Gate("trial-balance", "Trial Balance", true, true, "ok", "bad", 0m, 1),
            FinalAccountsPeriodCloseRules.Gate("balance-sheet", "Balance Sheet", true, false, "ok", "difference", 10m, 2),
            FinalAccountsPeriodCloseRules.Info("period-lock", "Lock", FinalAccountsPeriodCloseRules.Warning, "not requested", null, 3)
        };

        Assert.False(FinalAccountsPeriodCloseRules.CanClose(checklist));
        Assert.Equal("Block", FinalAccountsPeriodCloseRules.OverallStatus(checklist));
    }

    [Fact]
    public void OptionalWarningsDoNotBlockClose()
    {
        var checklist = new[]
        {
            FinalAccountsPeriodCloseRules.Gate("trial-balance", "Trial Balance", true, true, "ok", "bad", 0m, 1),
            FinalAccountsPeriodCloseRules.Info("period-lock", "Lock", FinalAccountsPeriodCloseRules.Warning, "not requested", null, 2)
        };

        Assert.True(FinalAccountsPeriodCloseRules.CanClose(checklist));
        Assert.Equal("Warning", FinalAccountsPeriodCloseRules.OverallStatus(checklist));
    }

    [Theory]
    [InlineData(FinalAccountsCloseRunStatus.Closed, true)]
    [InlineData(FinalAccountsCloseRunStatus.Reopened, false)]
    [InlineData(FinalAccountsCloseRunStatus.Draft, false)]
    public void OnlyClosedRunsCanReopen(FinalAccountsCloseRunStatus status, bool expected)
    {
        Assert.Equal(expected, FinalAccountsPeriodCloseRules.CanReopen(status));
    }

    [Fact]
    public void RunNumberPrefixSeparatesPeriodAndYearClose()
    {
        var date = new DateTime(2026, 3, 31);

        Assert.Equal("FPC-20260331", FinalAccountsPeriodCloseRules.BuildRunNumberPrefix(FinalAccountsCloseType.Period, date));
        Assert.Equal("FYC-20260331", FinalAccountsPeriodCloseRules.BuildRunNumberPrefix(FinalAccountsCloseType.Year, date));
    }
}
