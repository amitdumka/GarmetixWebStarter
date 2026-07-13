using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsStatementRules
{
    public const string DefaultTemplateCode = "GarmentRetailProfitLoss";
    public const string DefaultTemplateVersion = "v1";

    public static IReadOnlyList<FinalAccountsStatementTemplateNodeDto> ProfitLossTemplateNodes()
        => [
            Node("Revenue", "Revenue", "AccountGroup", 1, null, "CreditPositive", true, true, "Sales revenue before returns and discounts.", "SCH-PL-01", "Sales, tailoring and operating revenue accounts."),
            Node("SalesReturnsDiscount", "Sales returns and discounts", "AccountGroup", 2, null, "DebitPositive", false, true, "Contra income and discount presentation.", "SCH-PL-02", "Sale returns, credit notes and invoice discounts."),
            Node("NetRevenue", "Net revenue", "Formula", 3, "Revenue-SalesReturnsDiscount", "CreditPositive", true, false, "Revenue after returns and discounts.", "SCH-PL-03", "Formula node."),
            Node("OpeningInventory", "Opening inventory", "Policy", 4, null, "DebitPositive", false, false, "Perpetual weighted-average policy: no duplicate opening-stock P&L entry.", "SCH-PL-04", "Policy disclosure."),
            Node("PurchasesDirectCost", "Purchases and direct cost", "AccountGroup", 5, null, "DebitPositive", true, true, "Direct purchases, freight and direct operating costs.", "SCH-PL-05", "Purchase/direct cost accounts."),
            Node("ClosingInventory", "Closing inventory", "Policy", 6, null, "CreditPositive", false, false, "Perpetual weighted-average policy: inventory is carried through COGS movements.", "SCH-PL-06", "Policy disclosure."),
            Node("Cogs", "Cost of goods sold", "AccountGroup", 7, null, "DebitPositive", true, true, "COGS and stock-shortage movements.", "SCH-PL-07", "Inventory/COGS accounts."),
            Node("GrossProfit", "Gross profit", "Formula", 8, "NetRevenue-PurchasesDirectCost-Cogs", "CreditPositive", true, false, "Net revenue less direct cost and COGS.", "SCH-PL-08", "Formula node."),
            Node("OtherIncome", "Other income", "AccountGroup", 9, null, "CreditPositive", false, true, "Miscellaneous and non-operating income.", "SCH-PL-09", "Other income accounts."),
            Node("OperatingExpenses", "Operating expenses", "AccountGroup", 10, null, "DebitPositive", true, true, "Payroll, admin, indirect expenses and tailoring vendor cost.", "SCH-PL-10", "Operating expense accounts."),
            Node("Ebitda", "EBITDA", "Formula", 11, "GrossProfit+OtherIncome-OperatingExpenses", "CreditPositive", true, false, "Earnings before depreciation, finance cost and tax.", "SCH-PL-11", "Formula node."),
            Node("Depreciation", "Depreciation", "AccountGroup", 12, null, "DebitPositive", false, true, "Depreciation and amortisation expense.", "SCH-PL-12", "Depreciation accounts."),
            Node("FinanceCost", "Finance cost", "AccountGroup", 13, null, "DebitPositive", false, true, "Interest, loan charges and bank finance cost.", "SCH-PL-13", "Finance cost accounts."),
            Node("ProfitBeforeTax", "Profit before tax", "Formula", 14, "Ebitda-Depreciation-FinanceCost", "CreditPositive", true, false, "Profit before tax/provision.", "SCH-PL-14", "Formula node."),
            Node("TaxProvision", "Tax and provision", "AccountGroup", 15, null, "DebitPositive", false, true, "Income tax, provision and statutory P&L tax charges.", "SCH-PL-15", "Tax/provision accounts."),
            Node("ProfitAfterTax", "Profit after tax", "Formula", 16, "ProfitBeforeTax-TaxProvision", "CreditPositive", true, false, "Final retained profit for the selected period.", "SCH-PL-16", "Formula node.")
        ];

    public static FinalAccountsStatementTemplateDto ProfitLossTemplate()
        => new(
            DefaultTemplateCode,
            DefaultTemplateVersion,
            "Garment Retail Profit And Loss",
            "ProfitLoss",
            "Vertical",
            "Ones",
            HideZeroDefault: false,
            ProfitLossTemplateNodes());

    public static string NormalizeStatementView(string? view)
        => string.Equals(view, "Horizontal", StringComparison.OrdinalIgnoreCase) ? "Horizontal" : "Vertical";

    public static string NormalizeRoundingUnit(string? roundingUnit)
    {
        if (string.Equals(roundingUnit, "Lakhs", StringComparison.OrdinalIgnoreCase))
        {
            return "Lakhs";
        }

        if (string.Equals(roundingUnit, "Thousands", StringComparison.OrdinalIgnoreCase))
        {
            return "Thousands";
        }

        return "Ones";
    }

    public static decimal RoundingDivisor(string? roundingUnit)
        => NormalizeRoundingUnit(roundingUnit) switch
        {
            "Lakhs" => 100000m,
            "Thousands" => 1000m,
            _ => 1m
        };

    public static decimal RoundStatementValue(decimal value, string? roundingUnit)
        => FinalAccountsReportRules.RoundAmount(value / RoundingDivisor(roundingUnit));

    public static decimal Variance(decimal current, decimal previous)
        => FinalAccountsReportRules.RoundAmount(current - previous);

    public static decimal? VariancePercent(decimal current, decimal previous)
        => previous == 0m ? null : FinalAccountsReportRules.RoundAmount((current - previous) / Math.Abs(previous) * 100m);

    public static decimal? PercentOfSales(decimal value, decimal revenue)
        => revenue == 0m ? null : FinalAccountsReportRules.RoundAmount(value / Math.Abs(revenue) * 100m);

    public static string ClassifyProfitLossCategory(FinalAccountsAccount account, string? groupName)
    {
        var haystack = $"{account.Code} {account.Name} {groupName ?? string.Empty}".ToLowerInvariant();
        if (account.AccountType == FinalAccountsAccountType.Income)
        {
            if (ContainsAny(haystack, "return", "discount", "credit note"))
            {
                return "SalesReturnsDiscount";
            }

            if (ContainsAny(haystack, "other", "misc", "interest income", "stock excess", "excess"))
            {
                return "OtherIncome";
            }

            return "Revenue";
        }

        if (account.AccountType != FinalAccountsAccountType.Expense)
        {
            return string.Empty;
        }

        if (ContainsAny(haystack, "depreciation", "amortisation", "amortization"))
        {
            return "Depreciation";
        }

        if (ContainsAny(haystack, "finance", "interest", "loan", "bank charge", "bank charges"))
        {
            return "FinanceCost";
        }

        if (ContainsAny(haystack, "income tax", "tax provision", "provision"))
        {
            return "TaxProvision";
        }

        if (ContainsAny(haystack, "cogs", "cost of goods", "shortage"))
        {
            return "Cogs";
        }

        if (ContainsAny(haystack, "purchase", "direct cost", "freight", "landed"))
        {
            return "PurchasesDirectCost";
        }

        return "OperatingExpenses";
    }

    public static decimal SignedMovement(decimal debit, decimal credit, FinalAccountsAccountType accountType)
    {
        var amount = accountType == FinalAccountsAccountType.Income
            ? credit - debit
            : debit - credit;
        return FinalAccountsReportRules.RoundAmount(amount);
    }

    public static IReadOnlyList<FinalAccountsValidationIssueDto> ValidateProfitLossMappings(IReadOnlyDictionary<string, decimal> currentValues)
    {
        var required = new[] { "Revenue", "PurchasesDirectCost", "Cogs", "OperatingExpenses" };
        return required
            .Where(key => !currentValues.ContainsKey(key))
            .Select(key => new FinalAccountsValidationIssueDto(
                "Warning",
                "ProfitLossMappingMissing",
                $"Profit & Loss template node {key} has no mapped account movement in the selected scope/period.",
                null))
            .ToList();
    }

    public static decimal EvaluateFormula(string formula, IReadOnlyDictionary<string, decimal> values)
    {
        var total = 0m;
        var sign = 1m;
        var token = string.Empty;
        foreach (var ch in $"{formula}+")
        {
            if (ch == '+' || ch == '-')
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    total += sign * values.GetValueOrDefault(token.Trim());
                }

                sign = ch == '-' ? -1m : 1m;
                token = string.Empty;
                continue;
            }

            token += ch;
        }

        return FinalAccountsReportRules.RoundAmount(total);
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(haystack.Contains);

    private static FinalAccountsStatementTemplateNodeDto Node(
        string key,
        string label,
        string nodeType,
        int sortOrder,
        string? formula,
        string signRule,
        bool required,
        bool drillDown,
        string note,
        string scheduleReference,
        string mappingRule)
        => new(key, label, nodeType, sortOrder, null, formula, signRule, required, drillDown, note, scheduleReference, mappingRule);
}
