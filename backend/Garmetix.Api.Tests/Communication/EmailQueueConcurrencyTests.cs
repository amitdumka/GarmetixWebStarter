using Garmetix.Api.Communication;
using Garmetix.Api.Tests.Infrastructure;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Garmetix.Api.Tests.Communication;

/// <summary>
/// Postgres-only tests (skipped without GARMETIX_TEST_POSTGRES, same as every other test in
/// PostgresConcurrencyTests.cs) covering the two CM-02 domain guarantees that only a real
/// database can actually enforce: the unique IdempotencyKey index rejecting a concurrent
/// duplicate enqueue, and the Revision concurrency token rejecting a stale concurrent update.
/// </summary>
public sealed class EmailQueueConcurrencyTests
{
    [PostgresFact]
    public async Task DuplicateIdempotencyKey_OnlyOneConcurrentInsertSucceeds()
    {
        var idempotencyKey = $"test-{Guid.NewGuid():N}";
        var companyId = Guid.NewGuid();
        var results = new List<Task>();

        try
        {
            for (var i = 0; i < 5; i++)
            {
                results.Add(InsertAttemptAsync(idempotencyKey, companyId));
            }

            var outcomes = await Task.WhenAll(results.Select(async task =>
            {
                try
                {
                    await task;
                    return true;
                }
                catch (DbUpdateException)
                {
                    return false;
                }
            }));

            Assert.Equal(1, outcomes.Count(success => success));

            await using var verificationDb = CreateDbContext();
            var rowCount = await verificationDb.EmailQueueItems
                .IgnoreQueryFilters()
                .CountAsync(item => item.IdempotencyKey == idempotencyKey);
            Assert.Equal(1, rowCount);
        }
        finally
        {
            await CleanupAsync(idempotencyKey);
        }
    }

    [PostgresFact]
    public async Task StaleRevision_SecondConcurrentSaveThrowsConcurrencyException()
    {
        var idempotencyKey = $"test-{Guid.NewGuid():N}";
        var queueItemId = Guid.NewGuid();

        try
        {
            await using (var seedDb = CreateDbContext())
            {
                seedDb.EmailQueueItems.Add(new EmailQueueItem
                {
                    Id = queueItemId,
                    IdempotencyKey = idempotencyKey,
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    Status = EmailCatalog.QueueStatuses.Pending,
                    Subject = "Test",
                    HtmlBody = "<p>Test</p>",
                    Revision = 0,
                });
                await seedDb.SaveChangesAsync();
            }

            await using var firstDb = CreateDbContext();
            var firstCopy = await firstDb.EmailQueueItems.SingleAsync(item => item.Id == queueItemId);
            await using var secondDb = CreateDbContext();
            var secondCopy = await secondDb.EmailQueueItems.SingleAsync(item => item.Id == queueItemId);

            firstCopy.Status = EmailCatalog.QueueStatuses.Processing;
            await firstDb.SaveChangesAsync();

            secondCopy.Status = EmailCatalog.QueueStatuses.Cancelled;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());
        }
        finally
        {
            await CleanupAsync(idempotencyKey);
        }
    }

    private static async Task InsertAttemptAsync(string idempotencyKey, Guid companyId)
    {
        await using var db = CreateDbContext();
        db.EmailQueueItems.Add(new EmailQueueItem
        {
            CompanyId = companyId,
            IdempotencyKey = idempotencyKey,
            CorrelationId = Guid.NewGuid().ToString("N"),
            Status = EmailCatalog.QueueStatuses.Pending,
            Subject = "Test",
            HtmlBody = "<p>Test</p>",
        });
        await db.SaveChangesAsync();
    }

    private static GarmetixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmetixDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString)
            .Options;
        return new GarmetixDbContext(options);
    }

    private static async Task CleanupAsync(string idempotencyKey)
    {
        await using var db = CreateDbContext();
        await db.EmailQueueItems
            .IgnoreQueryFilters()
            .Where(item => item.IdempotencyKey == idempotencyKey)
            .ExecuteDeleteAsync();
    }
}
