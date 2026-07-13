using Garmetix.Api.FinalAccounts;
using Garmetix.Core.Models.FinalAccounts;
using Xunit;

namespace Garmetix.Api.Tests.FinalAccounts;

public sealed class FinalAccountsReportRulesTests
{
    [Fact]
    public void OpeningSignedBalanceHonorsNaturalBalance()
    {
        Assert.Equal(125m, FinalAccountsReportRules.OpeningSignedBalance(125m, FinalAccountsNaturalBalance.Debit));
        Assert.Equal(-125m, FinalAccountsReportRules.OpeningSignedBalance(125m, FinalAccountsNaturalBalance.Credit));
    }

    [Fact]
    public void SplitSignedBalanceReturnsDebitOrCreditColumn()
    {
        Assert.Equal((100m, 0m), FinalAccountsReportRules.SplitSignedBalance(100m));
        Assert.Equal((0m, 75m), FinalAccountsReportRules.SplitSignedBalance(-75m));
        Assert.Equal((0m, 0m), FinalAccountsReportRules.SplitSignedBalance(0m));
    }

    [Fact]
    public void BalanceStatusAllowsTinyRoundingNoise()
    {
        Assert.Equal("Balanced", FinalAccountsReportRules.BalanceStatus(0.004m));
        Assert.Equal("Difference", FinalAccountsReportRules.BalanceStatus(1m));
    }

    [Fact]
    public void PeriodKeySupportsMonthlyAndQuarterlyComparison()
    {
        var date = new DateTime(2026, 7, 13);

        Assert.Equal("2026-07", FinalAccountsReportRules.PeriodKey(date, "Monthly"));
        Assert.Equal("2026-Q3", FinalAccountsReportRules.PeriodKey(date, "Quarterly"));
    }

    [Fact]
    public void PageSizeIsBoundedForLedgerReports()
    {
        Assert.Equal(1, FinalAccountsReportRules.NormalizePage(0));
        Assert.Equal(FinalAccountsReportRules.DefaultPageSize, FinalAccountsReportRules.NormalizePageSize(null));
        Assert.Equal(FinalAccountsReportRules.MaxPageSize, FinalAccountsReportRules.NormalizePageSize(9999));
    }

    [Fact]
    public void ProfitLossTemplateExposesVersionedFormulaNodes()
    {
        var template = FinalAccountsStatementRules.ProfitLossTemplate();

        Assert.Equal("GarmentRetailProfitLoss", template.TemplateCode);
        Assert.Equal("v1", template.Version);
        Assert.Contains(template.Nodes, item => item.Key == "GrossProfit" && item.NodeType == "Formula");
        Assert.Contains(template.Nodes, item => item.Key == "ProfitAfterTax" && item.Formula == "ProfitBeforeTax-TaxProvision");
    }

    [Fact]
    public void FormulaEvaluationSupportsAddAndSubtract()
    {
        var values = new Dictionary<string, decimal>
        {
            ["Revenue"] = 1000m,
            ["Returns"] = 100m,
            ["Cogs"] = 400m
        };

        Assert.Equal(500m, FinalAccountsStatementRules.EvaluateFormula("Revenue-Returns-Cogs", values));
    }

    [Fact]
    public void ProfitLossClassificationUsesAccountShape()
    {
        var revenue = Account("4001", "Sales Revenue", FinalAccountsAccountType.Income);
        var returns = Account("4002", "Sales Return", FinalAccountsAccountType.Income);
        var cogs = Account("5101", "Cost Of Goods Sold", FinalAccountsAccountType.Expense);
        var finance = Account("5701", "Bank Interest", FinalAccountsAccountType.Expense);

        Assert.Equal("Revenue", FinalAccountsStatementRules.ClassifyProfitLossCategory(revenue, "Income"));
        Assert.Equal("SalesReturnsDiscount", FinalAccountsStatementRules.ClassifyProfitLossCategory(returns, "Income"));
        Assert.Equal("Cogs", FinalAccountsStatementRules.ClassifyProfitLossCategory(cogs, "Expenses"));
        Assert.Equal("FinanceCost", FinalAccountsStatementRules.ClassifyProfitLossCategory(finance, "Expenses"));
    }

    [Fact]
    public void StatementPercentagesAndRoundingAreStable()
    {
        Assert.Equal("Lakhs", FinalAccountsStatementRules.NormalizeRoundingUnit("lakhs"));
        Assert.Equal(1.25m, FinalAccountsStatementRules.RoundStatementValue(125000m, "Lakhs"));
        Assert.Equal(25m, FinalAccountsStatementRules.PercentOfSales(250m, 1000m));
        Assert.Equal(50m, FinalAccountsStatementRules.VariancePercent(150m, 100m));
    }

    [Fact]
    public void BalanceSheetTemplateSupportsEntityTypesAndBalanceFormula()
    {
        var proprietor = FinalAccountsStatementRules.BalanceSheetTemplate("Proprietorship");
        var partnership = FinalAccountsStatementRules.BalanceSheetTemplate("LLP");
        var company = FinalAccountsStatementRules.BalanceSheetTemplate("Company");

        Assert.Contains(proprietor.Nodes, item => item.Key == "TotalAssets" && item.Formula == "CurrentAssets+NonCurrentAssets");
        Assert.Contains(partnership.Nodes, item => item.Label.Contains("Partners", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(company.Nodes, item => item.Label.Contains("Share capital", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BalanceSheetClassificationFindsCurrentNonCurrentAndEquity()
    {
        Assert.Equal("CurrentAssets", FinalAccountsStatementRules.ClassifyBalanceSheetCategory(Account("1101", "Cash In Hand", FinalAccountsAccountType.Asset), "Cash"));
        Assert.Equal("NonCurrentAssets", FinalAccountsStatementRules.ClassifyBalanceSheetCategory(Account("1501", "Fixed Asset Furniture", FinalAccountsAccountType.Asset), "Assets"));
        Assert.Equal("CurrentLiabilities", FinalAccountsStatementRules.ClassifyBalanceSheetCategory(Account("2101", "Vendor Payable", FinalAccountsAccountType.Liability), "Payables"));
        Assert.Equal("NonCurrentLiabilities", FinalAccountsStatementRules.ClassifyBalanceSheetCategory(Account("2501", "Long Term Loan", FinalAccountsAccountType.Liability), "Loans"));
        Assert.Equal("CapitalEquity", FinalAccountsStatementRules.ClassifyBalanceSheetCategory(Account("3001", "Owner Capital", FinalAccountsAccountType.Equity), "Capital"));
    }

    [Fact]
    public void ScheduleAndCashFlowClassificationsUseAccountShape()
    {
        var debtor = Account("1201", "Customer Receivable", FinalAccountsAccountType.Asset);
        var gst = Account("2201", "GST Payable", FinalAccountsAccountType.Liability);
        var bank = Account("1102", "Bank Account", FinalAccountsAccountType.Asset);
        var asset = Account("1501", "Fixed Asset Computer", FinalAccountsAccountType.Asset);
        var loan = Account("2501", "Business Loan", FinalAccountsAccountType.Liability);

        Assert.Equal("DebtorAgeing", FinalAccountsStatementRules.ClassifySchedule("CurrentAssets", debtor, "Receivables"));
        Assert.Equal("GstTds", FinalAccountsStatementRules.ClassifySchedule("CurrentLiabilities", gst, "Tax"));
        Assert.True(FinalAccountsStatementRules.IsCashEquivalent(bank, "Bank"));
        Assert.Equal("Investing", FinalAccountsStatementRules.ClassifyCashFlowActivity(asset, "Fixed Assets"));
        Assert.Equal("Financing", FinalAccountsStatementRules.ClassifyCashFlowActivity(loan, "Loans"));
    }

    private static FinalAccountsAccount Account(string code, string name, FinalAccountsAccountType type)
        => new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            AccountType = type,
            NaturalBalance = type == FinalAccountsAccountType.Income
                ? FinalAccountsNaturalBalance.Credit
                : FinalAccountsNaturalBalance.Debit
        };
}
