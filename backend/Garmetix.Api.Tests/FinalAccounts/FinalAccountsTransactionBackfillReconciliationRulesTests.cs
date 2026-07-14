using Garmetix.Api.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsTransactionBackfillReconciliationRulesTests
{
    [Fact]
    public void EvidenceEndpointIsReadOnlyAndStageNamed()
    {
        Assert.Equal("BS19TransactionBackfillReconciliation", FinalAccountsTransactionBackfillReconciliationRules.StageName);
        Assert.True(FinalAccountsTransactionBackfillReconciliationRules.IsReadOnlyEvidenceEndpoint("/api/final-accounts/audit/transaction-backfill-reconciliation"));
        Assert.False(FinalAccountsTransactionBackfillReconciliationRules.IsReadOnlyEvidenceEndpoint("/api/final-accounts/backfill/live"));
        Assert.Contains("--stage=BS19TransactionBackfillReconciliation", FinalAccountsTransactionBackfillReconciliationRules.BackupRequirement);
    }

    [Fact]
    public void ParseModulesAllowsCommaSeparatedDistinctValues()
    {
        var modules = FinalAccountsTransactionBackfillReconciliationRules.ParseModules("Sales, Purchase,Sales,Payroll");

        Assert.NotNull(modules);
        Assert.Equal(["Sales", "Purchase", "Payroll"], modules);
    }

    [Fact]
    public void ParseModulesReturnsNullForDefaultModuleSet()
    {
        Assert.Null(FinalAccountsTransactionBackfillReconciliationRules.ParseModules(null));
        Assert.Null(FinalAccountsTransactionBackfillReconciliationRules.ParseModules("   "));
    }

    [Theory]
    [InlineData(0, 0, 0, "Balanced")]
    [InlineData(0, 2, 0, "Review")]
    [InlineData(0, 0, 1, "Review")]
    [InlineData(1.25, 0, 0, "Difference")]
    public void StatusForDifferenceSeparatesBalancedReviewAndDifference(decimal difference, int exceptionCount, int driftCount, string expected)
    {
        Assert.Equal(expected, FinalAccountsTransactionBackfillReconciliationRules.StatusForDifference(difference, exceptionCount, driftCount));
    }

    [Theory]
    [InlineData("Balanced", 0, "No backfill action suggested")]
    [InlineData("Balanced", 3, "Pending rows remain")]
    [InlineData("Review", 0, "Resolve drift")]
    [InlineData("Difference", 0, "Do not backfill")]
    public void SuggestedActionMatchesModuleStatus(string status, int pendingCount, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, FinalAccountsTransactionBackfillReconciliationRules.SuggestedActionForModule(status, pendingCount));
    }

    [Fact]
    public void ApprovalAndRollbackPlansRequireBackupAndTrialBalance()
    {
        Assert.Contains(FinalAccountsTransactionBackfillReconciliationRules.ApprovalGates, item => item.Detail.Contains("Trial Balance", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(FinalAccountsTransactionBackfillReconciliationRules.RollbackPlan, item => item.Detail.Contains("backup", StringComparison.OrdinalIgnoreCase));
    }
}
