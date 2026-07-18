using Garmetix.Api.Communication;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

public sealed class EmailRetryBackoffCalculatorTests
{
    [Fact]
    public void Compute_NoJitter_DoublesEachAttempt()
    {
        var baseDelay = TimeSpan.FromSeconds(10);
        var maxDelay = TimeSpan.FromHours(1);

        Assert.Equal(TimeSpan.FromSeconds(10), EmailRetryBackoffCalculator.Compute(1, baseDelay, maxDelay, 0.2, jitterFraction: 0));
        Assert.Equal(TimeSpan.FromSeconds(20), EmailRetryBackoffCalculator.Compute(2, baseDelay, maxDelay, 0.2, jitterFraction: 0));
        Assert.Equal(TimeSpan.FromSeconds(40), EmailRetryBackoffCalculator.Compute(3, baseDelay, maxDelay, 0.2, jitterFraction: 0));
    }

    [Fact]
    public void Compute_NeverExceedsMaxDelay_EvenWithPositiveJitter()
    {
        var result = EmailRetryBackoffCalculator.Compute(50, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3600), 0.2, jitterFraction: 1.0);
        Assert.True(result <= TimeSpan.FromSeconds(3600));
    }

    [Fact]
    public void Compute_NeverGoesNegative_WithMaximalNegativeJitter()
    {
        var result = EmailRetryBackoffCalculator.Compute(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(3600), 1.0, jitterFraction: -1.0);
        Assert.True(result >= TimeSpan.Zero);
    }

    [Fact]
    public void Compute_JitterFraction_ScalesWithinRatioBand()
    {
        var baseDelay = TimeSpan.FromSeconds(100);
        var withPositiveJitter = EmailRetryBackoffCalculator.Compute(1, baseDelay, TimeSpan.FromHours(1), 0.2, jitterFraction: 1.0);
        var withNoJitter = EmailRetryBackoffCalculator.Compute(1, baseDelay, TimeSpan.FromHours(1), 0.2, jitterFraction: 0);
        var withNegativeJitter = EmailRetryBackoffCalculator.Compute(1, baseDelay, TimeSpan.FromHours(1), 0.2, jitterFraction: -1.0);

        Assert.Equal(TimeSpan.FromSeconds(120), withPositiveJitter);
        Assert.Equal(TimeSpan.FromSeconds(100), withNoJitter);
        Assert.Equal(TimeSpan.FromSeconds(80), withNegativeJitter);
    }

    [Fact]
    public void Compute_TreatsAttemptCountBelowOneAsOne()
    {
        var withZero = EmailRetryBackoffCalculator.Compute(0, TimeSpan.FromSeconds(10), TimeSpan.FromHours(1), 0.2, jitterFraction: 0);
        var withOne = EmailRetryBackoffCalculator.Compute(1, TimeSpan.FromSeconds(10), TimeSpan.FromHours(1), 0.2, jitterFraction: 0);
        Assert.Equal(withOne, withZero);
    }

    [Fact]
    public void ComputeWithRandomJitter_StaysWithinDocumentedBounds()
    {
        var baseDelay = TimeSpan.FromSeconds(30);
        var maxDelay = TimeSpan.FromSeconds(3600);
        for (var i = 0; i < 50; i++)
        {
            var result = EmailRetryBackoffCalculator.ComputeWithRandomJitter(3, baseDelay, maxDelay, 0.2);
            Assert.InRange(result.TotalSeconds, 0, maxDelay.TotalSeconds);
        }
    }
}
