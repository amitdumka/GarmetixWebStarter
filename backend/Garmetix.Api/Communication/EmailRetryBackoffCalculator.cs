namespace Garmetix.Api.Communication;

/// <summary>
/// Exponential backoff with jitter for EmailQueueItem retries. Pure and deterministic given a
/// supplied jitter fraction, so it can be unit tested without relying on real randomness.
/// </summary>
public static class EmailRetryBackoffCalculator
{
    /// <param name="attemptCount">1-based count of attempts already made (the attempt that just failed).</param>
    /// <param name="jitterFraction">A value in [-1, 1] representing where in the jitter band this call lands; 0 = no jitter. Callers pass a random value in production and fixed values in tests.</param>
    public static TimeSpan Compute(int attemptCount, TimeSpan baseDelay, TimeSpan maxDelay, double jitterRatio, double jitterFraction)
    {
        if (attemptCount < 1)
        {
            attemptCount = 1;
        }

        var exponent = Math.Min(attemptCount - 1, 20); // guard against overflow on pathological attempt counts
        var rawSeconds = baseDelay.TotalSeconds * Math.Pow(2, exponent);
        var cappedSeconds = Math.Min(rawSeconds, maxDelay.TotalSeconds);

        var clampedJitterFraction = Math.Clamp(jitterFraction, -1.0, 1.0);
        var jitteredSeconds = cappedSeconds * (1 + (clampedJitterFraction * jitterRatio));
        var finalSeconds = Math.Clamp(jitteredSeconds, 0, maxDelay.TotalSeconds);

        return TimeSpan.FromSeconds(finalSeconds);
    }

    /// <summary>Production entry point - draws its own jitter from the shared Random.Shared source.</summary>
    public static TimeSpan ComputeWithRandomJitter(int attemptCount, TimeSpan baseDelay, TimeSpan maxDelay, double jitterRatio)
    {
        var jitterFraction = (Random.Shared.NextDouble() * 2) - 1; // uniform in [-1, 1]
        return Compute(attemptCount, baseDelay, maxDelay, jitterRatio, jitterFraction);
    }
}
