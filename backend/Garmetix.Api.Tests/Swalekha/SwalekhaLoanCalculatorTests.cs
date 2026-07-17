using Garmetix.Api.Swalekha;
using Xunit;

namespace Garmetix.Api.Tests.Swalekha;

public sealed class SwalekhaLoanCalculatorTests
{
    [Fact]
    public void CalculateEmi_ZeroInterest_SplitsPrincipalEvenly()
    {
        var emi = SwalekhaLoanCalculator.CalculateEmi(1200m, 0m, 12);

        Assert.Equal(100m, emi);
    }

    [Fact]
    public void CalculateEmi_StandardCase_MatchesKnownResult()
    {
        // 1,00,000 at 12% annual for 12 months is a textbook example - EMI is ~8,884.88.
        var emi = SwalekhaLoanCalculator.CalculateEmi(100000m, 12m, 12);

        Assert.InRange(emi, 8884.80m, 8884.95m);
    }

    [Fact]
    public void ProjectSchedule_FullyAmortizesToZeroUsingItsOwnEmi()
    {
        const decimal principal = 100000m;
        const decimal rate = 12m;
        const int tenure = 12;
        var emi = SwalekhaLoanCalculator.CalculateEmi(principal, rate, tenure);

        var schedule = SwalekhaLoanCalculator.ProjectSchedule(principal, rate, emi);

        Assert.Equal(tenure, schedule.Count);
        Assert.True(schedule[^1].ClosingBalance <= 0.01m, $"Expected the loan to fully amortize, ended at {schedule[^1].ClosingBalance}");

        var totalPrincipalPaid = schedule.Sum(r => r.PrincipalComponent);
        Assert.InRange(totalPrincipalPaid, principal - 1m, principal + 1m);
    }

    [Fact]
    public void ProjectSchedule_EmiBelowInterest_StopsImmediatelyRatherThanLoopingForever()
    {
        // At 12% annual on 100,000, month-1 interest alone is ~1000 - an EMI of 500 can never cover it.
        var schedule = SwalekhaLoanCalculator.ProjectSchedule(100000m, 12m, 500m);

        Assert.Empty(schedule);
    }

    [Fact]
    public void ProjectSchedule_LastPaymentClampsToExactRemainingBalance()
    {
        var schedule = SwalekhaLoanCalculator.ProjectSchedule(100000m, 12m, SwalekhaLoanCalculator.CalculateEmi(100000m, 12m, 12));

        Assert.All(schedule, row => Assert.True(row.ClosingBalance >= 0m, "Balance should never go negative"));
    }
}
