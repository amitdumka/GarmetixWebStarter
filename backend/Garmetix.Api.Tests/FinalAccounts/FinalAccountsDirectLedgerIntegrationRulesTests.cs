using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsDirectLedgerIntegrationRulesTests
{
    [Fact]
    public void EvidenceEndpointIsReadOnlyAndStageNamed()
    {
        Assert.Equal("BS20FinalAccountsLedgerIntegration", FinalAccountsDirectLedgerIntegrationRules.StageName);
        Assert.True(FinalAccountsDirectLedgerIntegrationRules.IsReadOnlyEvidenceEndpoint("/api/final-accounts/audit/direct-ledger-integration"));
        Assert.False(FinalAccountsDirectLedgerIntegrationRules.IsReadOnlyEvidenceEndpoint("/api/final-accounts/reports/direct-ledger/switch"));
        Assert.Contains("--stage=BS20FinalAccountsLedgerIntegration", FinalAccountsDirectLedgerIntegrationRules.BackupRequirement);
    }

    [Theory]
    [InlineData(0, "Balanced")]
    [InlineData(0.009, "Balanced")]
    [InlineData(0.02, "Difference")]
    [InlineData(-1.25, "Difference")]
    public void StatusForDifferenceUsesAccountingTolerance(decimal difference, string expected)
    {
        Assert.Equal(expected, FinalAccountsDirectLedgerIntegrationRules.StatusForDifference(difference));
    }

    [Theory]
    [InlineData(95, "SALES_ACCOUNTS", "AutoSuggested")]
    [InlineData(75, "DIRECT_EXPENSES", "Review")]
    [InlineData(40, "UNMAPPED", "ManualReview")]
    public void ClassificationStatusSeparatesAutoReviewAndManual(int confidence, string ruleCode, string expected)
    {
        Assert.Equal(expected, FinalAccountsDirectLedgerIntegrationRules.ClassificationStatus(confidence, ruleCode));
    }

    [Theory]
    [InlineData(100, "CAPITAL", "Use as direct-ledger")]
    [InlineData(75, "CURRENT_ASSETS", "Review classification")]
    [InlineData(0, "UNMAPPED", "Map this Books ledger group")]
    public void SuggestedActionMatchesClassificationRisk(int confidence, string ruleCode, string expectedPrefix)
    {
        Assert.StartsWith(expectedPrefix, FinalAccountsDirectLedgerIntegrationRules.SuggestedActionForClassification(confidence, ruleCode));
    }

    [Theory]
    [InlineData(120, "Asset", 120)]
    [InlineData(-80, "Liability", 80)]
    [InlineData(-50, "Income", 50)]
    [InlineData(40, "Expense", 40)]
    public void StatementAmountRespectsNaturalStatementSide(decimal signedDebitMinusCredit, string accountTypeName, decimal expected)
    {
        var accountType = Enum.Parse<FinalAccountsAccountType>(accountTypeName);

        Assert.Equal(expected, FinalAccountsDirectLedgerIntegrationRules.StatementAmount(signedDebitMinusCredit, accountType));
    }

    [Fact]
    public void PlansRequireBackupAndSourcePostingSmoke()
    {
        Assert.Contains(FinalAccountsDirectLedgerIntegrationRules.IntegrationPlan, item => item.Detail.Contains("backup", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(FinalAccountsDirectLedgerIntegrationRules.IntegrationPlan, item => item.Detail.Contains("Sales", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(FinalAccountsDirectLedgerIntegrationRules.RollbackPlan, item => item.Detail.Contains("exception mapping", StringComparison.OrdinalIgnoreCase));
    }
}
