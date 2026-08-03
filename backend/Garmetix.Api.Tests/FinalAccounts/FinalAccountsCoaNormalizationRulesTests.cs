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

    [Theory]
    [InlineData("Employees", LedgerCategory.Employees, "Loans & Advances (Asset)", "EMPLOYEE_ADVANCES", FinalAccountsAccountType.Asset)]
    [InlineData("No Group", LedgerCategory.UnCategory, "Suspense Account", "SUSPENSE", FinalAccountsAccountType.Liability)]
    [InlineData("Petty Expenses", LedgerCategory.Expenses, "Indirect Expenses", "INDIRECT_EXPENSES", FinalAccountsAccountType.Expense)]
    [InlineData("Store Expenses", LedgerCategory.Expenses, "Indirect Expenses", "INDIRECT_EXPENSES", FinalAccountsAccountType.Expense)]
    [InlineData("Snacks & Refreshments", LedgerCategory.IndirectExpenses, "Indirect Expenses", "INDIRECT_EXPENSES", FinalAccountsAccountType.Expense)]
    [InlineData("Loans - Secured", LedgerCategory.SecuredLoans, "Secured Loans", "SECURED_LOANS", FinalAccountsAccountType.Liability)]
    [InlineData("Loans - Unsecured", LedgerCategory.UnsecuredLoans, "Unsecured Loans", "UNSECURED_LOANS", FinalAccountsAccountType.Liability)]
    [InlineData("Suspense Account", LedgerCategory.SuspenseAccount, "Suspense Account", "SUSPENSE", FinalAccountsAccountType.Liability)]
    public void ApprovedLedgerGroupClassificationsUseTallyStyleEvidence(string name, LedgerCategory category, string primaryGroup, string ruleCode, FinalAccountsAccountType accountType)
    {
        var result = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup(name, category);

        Assert.Equal(primaryGroup, result.PrimaryGroup);
        Assert.Equal(ruleCode, result.RuleCode);
        Assert.Equal(accountType, result.AccountType);
        Assert.Equal("APPROVED_EXACT_NAME", result.MatchSource);
        Assert.True(result.Confidence >= 90);
    }

    [Theory]
    [InlineData("Current Assets", LedgerCategory.CurrentAssets, "Current Assets", FinalAccountsAccountType.Asset)]
    [InlineData("Current Liabilities", LedgerCategory.CurrentLiabilities, "Current Liabilities", FinalAccountsAccountType.Liability)]
    public void KeywordMatchingDoesNotMatchWordsInsideOtherWords(string name, LedgerCategory category, string primaryGroup, FinalAccountsAccountType accountType)
    {
        var result = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup(name, category);

        Assert.Equal(primaryGroup, result.PrimaryGroup);
        Assert.Equal(accountType, result.AccountType);
        Assert.NotEqual("INDIRECT_EXPENSES", result.RuleCode);
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
