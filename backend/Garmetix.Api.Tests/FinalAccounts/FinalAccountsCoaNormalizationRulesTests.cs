using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsCoaNormalizationRulesTests
{
    [Fact]
    public void PreviewEndpointIsReadOnlyAndStageNamed()
    {
        Assert.Equal("BS17IndianCOANormalization", FinalAccountsCoaNormalizationRules.StageName);
        Assert.True(FinalAccountsCoaNormalizationRules.IsReadOnlyPreviewEndpoint("/api/final-accounts/audit/coa-normalization"));
        Assert.False(FinalAccountsCoaNormalizationRules.IsReadOnlyPreviewEndpoint("/api/final-accounts/coa/normalize"));
        Assert.Contains("--stage=BS17IndianCOANormalization", FinalAccountsCoaNormalizationRules.BackupRequirement);
    }

    [Theory]
    [InlineData("  Bank-Accounts ", "BANK ACCOUNTS")]
    [InlineData("GST/Output_CGST", "GST OUTPUT CGST")]
    [InlineData("Cash-in-Hand", "CASH IN HAND")]
    public void NormalizeNameProducesStableComparisonKeys(string input, string expected)
    {
        Assert.Equal(expected, FinalAccountsCoaNormalizationRules.NormalizeName(input));
    }

    [Fact]
    public void LedgerCategoryMapsToTallyPrimaryGroup()
    {
        var result = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup("Main Bank", LedgerCategory.BankAccounts);

        Assert.Equal("Bank Accounts", result.PrimaryGroup);
        Assert.Equal(FinalAccountsAccountType.Asset, result.AccountType);
        Assert.Equal(FinalAccountsNaturalBalance.Debit, result.NaturalBalance);
        Assert.True(result.Confidence >= 90);
    }

    [Fact]
    public void KeywordOverridesGenericCategory()
    {
        var result = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup("Output GST Payable", LedgerCategory.CurrentLiabilities);

        Assert.Equal("Duties & Taxes", result.PrimaryGroup);
        Assert.Equal(FinalAccountsAccountType.Liability, result.AccountType);
    }

    [Fact]
    public void UnknownGroupRequiresManualClassification()
    {
        var result = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup("Misc Bucket", LedgerCategory.Credit);

        Assert.Equal("Unmapped", result.PrimaryGroup);
        Assert.Equal(0, result.Confidence);
        Assert.Equal("UNMAPPED", result.MatchSource);
    }

    [Fact]
    public void ControlAccountsIncludeCriticalOperationalMappings()
    {
        var keys = FinalAccountsCoaNormalizationRules.ControlAccounts.Select(item => item.MappingKey).ToArray();

        Assert.Contains("CUSTOMER.RECEIVABLE", keys);
        Assert.Contains("VENDOR.PAYABLE", keys);
        Assert.Contains("PAYMENT.CASH", keys);
        Assert.Contains("GST.OUTPUT_CGST", keys);
        Assert.Contains("INVENTORY.STOCK", keys);
        Assert.Contains("PAYROLL.PAYABLE", keys);
    }

    [Fact]
    public void MigrationAndRollbackPlansAreReviewFirst()
    {
        Assert.Contains(FinalAccountsCoaNormalizationRules.MigrationPlan, item => item.Detail.Contains("review", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(FinalAccountsCoaNormalizationRules.RollbackPlan, item => item.Detail.Contains("backup", StringComparison.OrdinalIgnoreCase));
    }
}
