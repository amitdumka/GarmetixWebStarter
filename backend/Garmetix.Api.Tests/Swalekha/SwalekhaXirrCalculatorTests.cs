using Garmetix.Api.Swalekha;
using Xunit;

namespace Garmetix.Api.Tests.Swalekha;

public sealed class SwalekhaXirrCalculatorTests
{
    [Fact]
    public void SingleYearRoundTrip_Returns20Percent()
    {
        // Invest 1000, get back 1200 exactly one year later - a clean, hand-verifiable XIRR of 20%
        // (NPV(r) = -1000 + 1200/(1+r) = 0  =>  1+r = 1.2  =>  r = 0.2).
        var cashFlows = new List<(DateTime Date, decimal Amount)>
        {
            (new DateTime(2025, 1, 1), -1000m),
            (new DateTime(2026, 1, 1), 1200m)
        };

        var ok = SwalekhaXirrCalculator.TryCalculate(cashFlows, out var rate);

        Assert.True(ok);
        Assert.InRange(rate, 0.195m, 0.205m);
    }

    [Fact]
    public void MultipleSipInstallmentsPlusCurrentValue_ReturnsPositiveRate()
    {
        // Three monthly SIP installments of 1000 each, current value 3400 six months after the
        // first installment - a genuinely profitable position, XIRR should come back positive.
        var cashFlows = new List<(DateTime Date, decimal Amount)>
        {
            (new DateTime(2025, 1, 1), -1000m),
            (new DateTime(2025, 2, 1), -1000m),
            (new DateTime(2025, 3, 1), -1000m),
            (new DateTime(2025, 7, 1), 3400m)
        };

        var ok = SwalekhaXirrCalculator.TryCalculate(cashFlows, out var rate);

        Assert.True(ok);
        Assert.True(rate > 0m, $"Expected a positive return, got {rate}");
    }

    [Fact]
    public void LossPosition_ReturnsNegativeRate()
    {
        var cashFlows = new List<(DateTime Date, decimal Amount)>
        {
            (new DateTime(2025, 1, 1), -1000m),
            (new DateTime(2026, 1, 1), 800m)
        };

        var ok = SwalekhaXirrCalculator.TryCalculate(cashFlows, out var rate);

        Assert.True(ok);
        Assert.True(rate < 0m, $"Expected a negative return, got {rate}");
    }

    [Fact]
    public void OnlyOutflows_ReturnsFalse()
    {
        var cashFlows = new List<(DateTime Date, decimal Amount)>
        {
            (new DateTime(2025, 1, 1), -1000m),
            (new DateTime(2025, 2, 1), -500m)
        };

        var ok = SwalekhaXirrCalculator.TryCalculate(cashFlows, out _);

        Assert.False(ok);
    }

    [Fact]
    public void SingleCashFlow_ReturnsFalse()
    {
        var cashFlows = new List<(DateTime Date, decimal Amount)> { (new DateTime(2025, 1, 1), -1000m) };

        var ok = SwalekhaXirrCalculator.TryCalculate(cashFlows, out _);

        Assert.False(ok);
    }
}
