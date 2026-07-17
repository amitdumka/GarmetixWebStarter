using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>Tracks a per-provider, per-company, per-day send/fail counter against EmailProviderConfiguration.DailyRateLimit.</summary>
public sealed class EmailRateLimitService(GarmetixDbContext db)
{
    public async Task<bool> IsDailyLimitExceededAsync(Guid? providerId, Guid? companyId, int? dailyLimit, CancellationToken cancellationToken)
    {
        if (dailyLimit is null)
        {
            return false;
        }

        var periodKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var counter = await db.EmailUsageCounters.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ProviderId == providerId && c.CompanyId == companyId && c.PeriodKey == periodKey, cancellationToken);

        return counter is not null && counter.SentCount >= dailyLimit.Value;
    }

    public async Task RecordAttemptAsync(Guid? providerId, Guid? companyId, bool wasSuccess, CancellationToken cancellationToken)
    {
        var periodKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var counter = await db.EmailUsageCounters
            .FirstOrDefaultAsync(c => c.ProviderId == providerId && c.CompanyId == companyId && c.PeriodKey == periodKey, cancellationToken);

        if (counter is null)
        {
            counter = new EmailUsageCounter { ProviderId = providerId, CompanyId = companyId, PeriodKey = periodKey };
            db.EmailUsageCounters.Add(counter);
        }

        if (wasSuccess)
        {
            counter.SentCount++;
        }
        else
        {
            counter.FailedCount++;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (db.Entry(counter).State == EntityState.Added)
        {
            // Lost a race against a concurrent worker inserting the same (ProviderId,
            // CompanyId, PeriodKey) counter row for the first time today - usage counting is
            // best-effort rate-limiting, not a financial ledger, so drop this increment rather
            // than fail the whole send pipeline over a lost race on a day-bucket row.
            db.Entry(counter).State = EntityState.Detached;
        }
    }
}
