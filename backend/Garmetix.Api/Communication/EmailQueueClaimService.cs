using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Communication;

/// <summary>
/// Postgres-safe atomic multi-worker claim for EmailQueueItem, using a single UPDATE ...
/// WHERE Id IN (SELECT ... FOR UPDATE SKIP LOCKED) ... RETURNING * statement (mirrors the
/// "FOR UPDATE SKIP LOCKED" pattern documented in communication-mail-architecture.md section
/// "Queue consistency", tested for real concurrency in EmailQueueClaimConcurrencyTests).
/// Runs as one round trip so two workers can never claim the same row, without needing an
/// explicit application-level transaction or advisory lock.
/// </summary>
public sealed class EmailQueueClaimService(GarmetixDbContext db)
{
    public async Task<List<EmailQueueItem>> ClaimBatchAsync(int batchSize, TimeSpan leaseDuration, string workerOwner, CancellationToken cancellationToken)
    {
        var leaseUntil = DateTime.UtcNow.Add(leaseDuration);
        var claimableStatuses = new[]
        {
            EmailCatalog.QueueStatuses.Pending,
            EmailCatalog.QueueStatuses.Deferred,
            EmailCatalog.QueueStatuses.Failed,
        };

        var claimed = await db.EmailQueueItems.FromSqlInterpolated($"""
            UPDATE "EmailQueueItems"
            SET "Status" = {EmailCatalog.QueueStatuses.Processing},
                "ProcessingLeaseUntilUtc" = {leaseUntil},
                "ProcessingLeaseOwner" = {workerOwner},
                "AttemptCount" = "AttemptCount" + 1,
                "UpdatedAt" = now()
            WHERE "Id" IN (
                SELECT "Id" FROM "EmailQueueItems"
                WHERE "Deleted" = false
                  AND "Status" = ANY({claimableStatuses})
                  AND ("ScheduledForUtc" IS NULL OR "ScheduledForUtc" <= now())
                  AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc" <= now())
                ORDER BY "NextAttemptAtUtc" NULLS FIRST, "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *
            """).ToListAsync(cancellationToken);

        return claimed;
    }

    /// <summary>Resets any Processing item whose lease expired (worker crashed/killed mid-send) back to Pending so another worker can retry it, or to DeadLetter if it already exhausted its attempt budget.</summary>
    public async Task<int> RecoverStaleLeasesAsync(CancellationToken cancellationToken)
    {
        var staleItems = await db.EmailQueueItems
            .Where(item => !item.Deleted
                && item.Status == EmailCatalog.QueueStatuses.Processing
                && item.ProcessingLeaseUntilUtc != null
                && item.ProcessingLeaseUntilUtc < DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var item in staleItems)
        {
            item.ProcessingLeaseOwner = null;
            item.ProcessingLeaseUntilUtc = null;
            if (item.AttemptCount >= item.MaxAttempts)
            {
                item.Status = EmailCatalog.QueueStatuses.DeadLetter;
                item.LastErrorCode ??= "StaleProcessingLease";
                item.LastErrorMessage ??= "Worker did not complete processing before its lease expired and attempts were exhausted.";
            }
            else
            {
                item.Status = EmailCatalog.QueueStatuses.Pending;
            }
        }

        if (staleItems.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return staleItems.Count;
    }
}
