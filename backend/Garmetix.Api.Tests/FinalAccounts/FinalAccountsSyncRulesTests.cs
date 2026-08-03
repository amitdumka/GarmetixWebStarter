using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsSyncRulesTests
{
    [Fact]
    public void NormalizeModulesDefaultsToSupportedSet()
    {
        var modules = FinalAccountsSyncRules.NormalizeModules(null);

        Assert.Contains("Sales", modules);
        Assert.Contains("Purchase", modules);
        Assert.Contains("CashBank", modules);
        Assert.Contains("Gst", modules);
        Assert.Contains("Inventory", modules);
        Assert.Contains("Payroll", modules);
    }

    [Fact]
    public void NormalizeModulesDeduplicatesAliases()
    {
        var modules = FinalAccountsSyncRules.NormalizeModules(["sales", "SALE", "cash-bank", "payment"]);

        Assert.Equal(["CashBank", "Sales"], modules);
    }

    [Fact]
    public void IdempotencyKeyIsStableAndBounded()
    {
        var longKey = new string('x', 200);

        var normalized = FinalAccountsSyncRules.NormalizeIdempotencyKey($" {longKey} ");

        Assert.Equal(160, normalized.Length);
        Assert.Equal(normalized, FinalAccountsSyncRules.NormalizeIdempotencyKey(normalized));
    }

    [Fact]
    public void JobNumberUsesIdempotencyKeyForRerun()
    {
        var now = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

        var first = FinalAccountsSyncRules.BuildJobNumber(now, "manual-bs05");
        var retry = FinalAccountsSyncRules.BuildJobNumber(now.AddHours(1), " manual-bs05 ");

        Assert.Equal(first, retry);
    }

    [Fact]
    public void StatusForSourceMarksExistingAndDrift()
    {
        Assert.Equal(FinalAccountsSyncItemStatus.Pending, FinalAccountsSyncRules.StatusForSource(alreadyLinked: false, drifted: false));
        Assert.Equal(FinalAccountsSyncItemStatus.SkippedExisting, FinalAccountsSyncRules.StatusForSource(alreadyLinked: true, drifted: false));
        Assert.Equal(FinalAccountsSyncItemStatus.Drifted, FinalAccountsSyncRules.StatusForSource(alreadyLinked: true, drifted: true));
    }

    [Fact]
    public void InventoryAdapterKeysTreatVyaparSaleImportAsSaleCogs()
    {
        var adapters = FinalAccountsSyncRules.InventoryAdapterKeys(["VyaparImportHistoricalStockBridgeIn", "VyaparSaleImport"]);

        Assert.Equal(["sale-cogs"], adapters);
    }

    [Fact]
    public void InventoryBackfillExcludesOpeningMigrationAndZeroValueRows()
    {
        Assert.True(FinalAccountsSyncRules.ShouldExcludeInventoryBackfillCandidate(["RegularUnbilledOpeningStockImport"], 38195m));
        Assert.True(FinalAccountsSyncRules.ShouldExcludeInventoryBackfillCandidate(["StockOperationDocument"], 0m));
        Assert.False(FinalAccountsSyncRules.ShouldExcludeInventoryBackfillCandidate(["VyaparSaleImport"], 135537.52m));
    }

    [Fact]
    public void PartialFailurePolicySupportsStopOrContinue()
    {
        Assert.False(FinalAccountsSyncRules.ShouldContinueAfterFailure(stopOnError: true, failedCount: 1));
        Assert.True(FinalAccountsSyncRules.ShouldContinueAfterFailure(stopOnError: false, failedCount: 3));
        Assert.True(FinalAccountsSyncRules.ShouldContinueAfterFailure(stopOnError: true, failedCount: 0));
    }

    [Fact]
    public void RetryDelayBacksOffByAttempt()
    {
        var now = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(now.AddSeconds(60), FinalAccountsSyncRules.NextRetryAt(now, attemptCount: 1, retryDelaySeconds: 60));
        Assert.Equal(now.AddSeconds(180), FinalAccountsSyncRules.NextRetryAt(now, attemptCount: 3, retryDelaySeconds: 60));
    }
}
