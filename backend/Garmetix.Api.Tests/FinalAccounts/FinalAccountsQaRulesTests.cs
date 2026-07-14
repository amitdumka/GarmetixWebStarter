using Garmetix.Api.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsQaRulesTests
{
    [Fact]
    public void LargeLedgerPaginationIsBoundedForQaRuns()
    {
        Assert.Equal(1, FinalAccountsReportRules.NormalizePage(-50));
        Assert.Equal(FinalAccountsReportRules.MaxPageSize, FinalAccountsReportRules.NormalizePageSize(10_000));
    }

    [Fact]
    public void BackfillBatchResumeCheckpointIsStableAcrossModuleOrder()
    {
        var from = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc);

        var first = FinalAccountsSyncRules.BuildResumeCheckpoint(["purchase", "sales"], from, to);
        var retry = FinalAccountsSyncRules.BuildResumeCheckpoint(["Sales", "Purchase"], from, to);

        Assert.Equal(first, retry);
        Assert.Contains("Purchase,Sales", first);
        Assert.Contains(from.ToString("O"), first);
        Assert.Contains(to.ToString("O"), first);
    }

    [Fact]
    public void LargeExportStreamingQaFlagsOversizedInMemoryPackages()
    {
        Assert.False(FinalAccountsQaRules.IsLargeExport(FinalAccountsQaRules.LargeExportWarningBytes));
        Assert.True(FinalAccountsQaRules.IsLargeExport(FinalAccountsQaRules.LargeExportWarningBytes + 1));
    }

    [Fact]
    public void ConcurrentPostingQaUsesStableIdempotencyKey()
    {
        var incoming = FinalAccountsJournalRules.NormalizeIdempotencyKey(" source:sales:invoice-42 ");
        var duplicate = FinalAccountsJournalRules.FindDuplicateIdempotencyKey("SOURCE:SALES:INVOICE-42", [incoming]);

        Assert.Equal(incoming, duplicate);
    }

    [Fact]
    public void FeatureDisabledRegressionExpectsForbiddenSetupRedirect()
    {
        Assert.Equal("403:/final-accounts/setup", FinalAccountsQaRules.FeatureDisabledStatus());
    }

    [Fact]
    public void BrowserRouteAndKeyboardQaCoverFinalAccountsWorkspaces()
    {
        Assert.Contains("/final-accounts/chart-of-accounts", FinalAccountsQaRules.RequiredBrowserRoutes);
        Assert.Contains("/final-accounts/general-ledger", FinalAccountsQaRules.RequiredKeyboardRoutes);
        Assert.Contains("/final-accounts/reports", FinalAccountsQaRules.RequiredKeyboardRoutes);
        Assert.Contains("/final-accounts/exchange", FinalAccountsQaRules.RequiredKeyboardRoutes);
    }

    [Fact]
    public void MigrationReviewRejectsDestructiveOperations()
    {
        Assert.True(FinalAccountsQaRules.IsAdditiveMigrationLine("migrationBuilder.CreateTable("));
        Assert.True(FinalAccountsQaRules.IsAdditiveMigrationLine("migrationBuilder.CreateIndex("));
        Assert.True(FinalAccountsQaRules.IsAdditiveMigrationLine("migrationBuilder.Sql(UniqueScopeIndexSql("));
        Assert.False(FinalAccountsQaRules.IsAdditiveMigrationLine("migrationBuilder.DropTable("));
        Assert.False(FinalAccountsQaRules.IsAdditiveMigrationLine("migrationBuilder.RenameColumn("));
    }

    [Fact]
    public void ProductionHostChangesAreOutOfScopeForBalanceSheetQa()
    {
        Assert.False(FinalAccountsQaRules.IsProductionHostChange("backend/Garmetix.Api/FinalAccounts/FinalAccountsQaRules.cs"));
        Assert.True(FinalAccountsQaRules.IsProductionHostChange("frontend/modular/deploy/srp-cloudflare-activate.sh"));
        Assert.True(FinalAccountsQaRules.IsProductionHostChange("frontend/modular/.env.production"));
    }
}
