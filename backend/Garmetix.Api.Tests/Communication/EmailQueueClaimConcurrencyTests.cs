using System.Collections.Concurrent;
using Garmetix.Api.Communication;
using Garmetix.Api.Tests.Infrastructure;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

/// <summary>
/// Postgres-only (skipped without GARMETIX_TEST_POSTGRES, same as PostgresConcurrencyTests.cs)
/// - proves the FOR UPDATE SKIP LOCKED claim in EmailQueueClaimService never lets two
/// concurrent "workers" claim the same EmailQueueItem, and that stale lease recovery brings a
/// crashed worker's claimed-but-unfinished item back into rotation.
/// </summary>
public sealed class EmailQueueClaimConcurrencyTests
{
    [PostgresFact]
    public async Task ClaimBatchAsync_NeverDoubleClaimsAcrossConcurrentWorkers()
    {
        const int itemCount = 12;
        const int workerCount = 4;
        var correlationMarker = $"claim-test-{Guid.NewGuid():N}";
        var itemIds = new List<Guid>();

        try
        {
            await using (var seedDb = CreateDbContext())
            {
                for (var i = 0; i < itemCount; i++)
                {
                    var item = new EmailQueueItem
                    {
                        IdempotencyKey = $"{correlationMarker}-{i}",
                        CorrelationId = correlationMarker,
                        Status = EmailCatalog.QueueStatuses.Pending,
                        Subject = "Test",
                        HtmlBody = "<p>Test</p>",
                    };
                    seedDb.EmailQueueItems.Add(item);
                    itemIds.Add(item.Id);
                }
                await seedDb.SaveChangesAsync();
            }

            var claimedIds = new ConcurrentBag<Guid>();
            await Task.WhenAll(Enumerable.Range(0, workerCount).Select(async workerIndex =>
            {
                await using var db = CreateDbContext();
                var claimService = new EmailQueueClaimService(db);
                var claimed = await claimService.ClaimBatchAsync(itemCount, TimeSpan.FromMinutes(5), $"worker-{workerIndex}", CancellationToken.None);
                foreach (var item in claimed)
                {
                    claimedIds.Add(item.Id);
                }
            }));

            Assert.Equal(itemCount, claimedIds.Count);
            Assert.Equal(itemCount, claimedIds.Distinct().Count());
            Assert.Equal(itemIds.OrderBy(id => id), claimedIds.OrderBy(id => id));

            await using var verificationDb = CreateDbContext();
            var processingCount = await verificationDb.EmailQueueItems
                .Where(item => item.CorrelationId == correlationMarker && item.Status == EmailCatalog.QueueStatuses.Processing)
                .CountAsync();
            Assert.Equal(itemCount, processingCount);
        }
        finally
        {
            await CleanupAsync(itemIds);
        }
    }

    [PostgresFact]
    public async Task RecoverStaleLeasesAsync_ReturnsExpiredLeaseToPendingWhenAttemptsRemain()
    {
        var itemIds = new List<Guid>();

        try
        {
            Guid itemId;
            await using (var seedDb = CreateDbContext())
            {
                var item = new EmailQueueItem
                {
                    IdempotencyKey = $"stale-lease-{Guid.NewGuid():N}",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Status = EmailCatalog.QueueStatuses.Processing,
                    Subject = "Test",
                    HtmlBody = "<p>Test</p>",
                    AttemptCount = 1,
                    MaxAttempts = 8,
                    ProcessingLeaseUntilUtc = DateTime.UtcNow.AddMinutes(-5),
                    ProcessingLeaseOwner = "crashed-worker",
                };
                seedDb.EmailQueueItems.Add(item);
                await seedDb.SaveChangesAsync();
                itemId = item.Id;
                itemIds.Add(itemId);
            }

            await using var db = CreateDbContext();
            var claimService = new EmailQueueClaimService(db);
            var recoveredCount = await claimService.RecoverStaleLeasesAsync(CancellationToken.None);
            Assert.True(recoveredCount >= 1);

            await using var verificationDb = CreateDbContext();
            var recovered = await verificationDb.EmailQueueItems.SingleAsync(item => item.Id == itemId);
            Assert.Equal(EmailCatalog.QueueStatuses.Pending, recovered.Status);
            Assert.Null(recovered.ProcessingLeaseOwner);
            Assert.Null(recovered.ProcessingLeaseUntilUtc);
        }
        finally
        {
            await CleanupAsync(itemIds);
        }
    }

    [PostgresFact]
    public async Task RecoverStaleLeasesAsync_MovesToDeadLetterWhenAttemptsExhausted()
    {
        var itemIds = new List<Guid>();

        try
        {
            Guid itemId;
            await using (var seedDb = CreateDbContext())
            {
                var item = new EmailQueueItem
                {
                    IdempotencyKey = $"stale-lease-exhausted-{Guid.NewGuid():N}",
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Status = EmailCatalog.QueueStatuses.Processing,
                    Subject = "Test",
                    HtmlBody = "<p>Test</p>",
                    AttemptCount = 8,
                    MaxAttempts = 8,
                    ProcessingLeaseUntilUtc = DateTime.UtcNow.AddMinutes(-5),
                    ProcessingLeaseOwner = "crashed-worker",
                };
                seedDb.EmailQueueItems.Add(item);
                await seedDb.SaveChangesAsync();
                itemId = item.Id;
                itemIds.Add(itemId);
            }

            await using var db = CreateDbContext();
            var claimService = new EmailQueueClaimService(db);
            await claimService.RecoverStaleLeasesAsync(CancellationToken.None);

            await using var verificationDb = CreateDbContext();
            var recovered = await verificationDb.EmailQueueItems.SingleAsync(item => item.Id == itemId);
            Assert.Equal(EmailCatalog.QueueStatuses.DeadLetter, recovered.Status);
        }
        finally
        {
            await CleanupAsync(itemIds);
        }
    }

    private static GarmetixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmetixDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString)
            .Options;
        return new GarmetixDbContext(options);
    }

    private static async Task CleanupAsync(IReadOnlyCollection<Guid> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        await using var db = CreateDbContext();
        await db.EmailQueueItems.IgnoreQueryFilters().Where(item => itemIds.Contains(item.Id)).ExecuteDeleteAsync();
    }
}
