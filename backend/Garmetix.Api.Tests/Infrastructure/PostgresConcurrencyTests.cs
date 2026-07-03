using System.Collections.Concurrent;
using Garmetix.Api.Numbering;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Garmetix.Api.Tests.Infrastructure;

public sealed class PostgresConcurrencyTests
{
    [PostgresFact]
    public async Task DocumentSequence_SerializesConcurrentNumberRequests()
    {
        const int requestCount = 8;
        var companyId = Guid.NewGuid();
        var storeGroupId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var documentType = $"ConcurrencyTest-{Guid.NewGuid():N}";
        var sequenceDate = new DateTime(2026, 6, 15);
        var numbers = new ConcurrentBag<string>();

        try
        {
            await Task.WhenAll(Enumerable.Range(0, requestCount).Select(async _ =>
            {
                await using var db = CreateDbContext();
                await using var transaction = await db.Database.BeginTransactionAsync();
                var number = await DocumentNumberGenerator.NextAsync(
                    db,
                    companyId,
                    storeGroupId,
                    storeId,
                    documentType,
                    "TST",
                    sequenceDate,
                    CancellationToken.None);
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                numbers.Add(number);
            }));

            var numericParts = numbers
                .Select(number => int.Parse(number.Split('-').Last()))
                .OrderBy(number => number)
                .ToArray();

            Assert.Equal(requestCount, numbers.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(Enumerable.Range(1, requestCount), numericParts);

            await using var verificationDb = CreateDbContext();
            var sequenceRows = await verificationDb.DocumentSequences
                .Where(item =>
                    item.CompanyId == companyId &&
                    item.StoreGroupId == storeGroupId &&
                    item.StoreId == storeId &&
                    item.DocumentType == documentType &&
                    item.SequenceDate == sequenceDate)
                .ToListAsync();

            var sequence = Assert.Single(sequenceRows);
            Assert.Equal(requestCount, sequence.LastNumber);
        }
        finally
        {
            await DeleteTestSequencesAsync(companyId);
        }
    }

    [PostgresFact]
    public async Task StockLock_BlocksSameStockUntilOwningTransactionCommits()
    {
        var companyId = Guid.NewGuid();
        var storeGroupId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var secondLockStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var firstDb = CreateDbContext();
        await using var firstTransaction = await firstDb.Database.BeginTransactionAsync();
        await DocumentNumberGenerator.LockStockKeyAsync(
            firstDb,
            companyId,
            storeGroupId,
            storeId,
            productId,
            "LOCK-TEST",
            CancellationToken.None);

        var secondLock = Task.Run(async () =>
        {
            await using var secondDb = CreateDbContext();
            await using var secondTransaction = await secondDb.Database.BeginTransactionAsync();
            secondLockStarted.SetResult();
            await DocumentNumberGenerator.LockStockKeyAsync(
                secondDb,
                companyId,
                storeGroupId,
                storeId,
                productId,
                "LOCK-TEST",
                CancellationToken.None);
            await secondTransaction.CommitAsync();
        });

        await secondLockStarted.Task;
        await Task.Delay(150);
        Assert.False(secondLock.IsCompleted);

        await firstTransaction.CommitAsync();
        await secondLock.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [PostgresFact]
    public async Task SequenceAndStockLocks_RejectCallsWithoutTransaction()
    {
        await using var db = CreateDbContext();

        var sequenceException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DocumentNumberGenerator.NextAsync(
                db,
                Guid.NewGuid(),
                null,
                null,
                "NoTransaction",
                "NO",
                DateTime.Today,
                CancellationToken.None));
        var stockException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DocumentNumberGenerator.LockStockKeyAsync(
                db,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "NO-TRANSACTION",
                CancellationToken.None));

        Assert.Contains("active database transaction", sequenceException.Message);
        Assert.Contains("active database transaction", stockException.Message);
    }

    private static GarmetixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GarmetixDbContext>()
            .UseNpgsql(PostgresTestDatabase.ConnectionString)
            .Options;
        return new GarmetixDbContext(options);
    }

    private static async Task DeleteTestSequencesAsync(Guid companyId)
    {
        await using var db = CreateDbContext();
        await db.DocumentSequences
            .IgnoreQueryFilters()
            .Where(item => item.CompanyId == companyId)
            .ExecuteDeleteAsync();
    }
}
