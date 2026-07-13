using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsStatementRules
{
    public const string DefaultTemplateCode = "GarmentRetailProfitLoss";
    public const string DefaultTemplateVersion = "v1";
    public const string BalanceSheetTemplateVersion = "v1";

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

    public static FinalAccountsStatementTemplateDto BalanceSheetTemplate(string? entityType)
    {
        var normalized = NormalizeEntityType(entityType);
        var name = normalized switch
        {
            "Partnership" => "Partnership/LLP Balance Sheet",
            "Company" => "Company Balance Sheet",
            _ => "Non-Corporate Balance Sheet"
        };
        return new(
            $"GarmentRetailBalanceSheet{normalized}",
            BalanceSheetTemplateVersion,
            name,
            "BalanceSheet",
            "Vertical",
            "Ones",
            HideZeroDefault: false,
            [
                Node("CurrentAssets", "Current assets", "AccountGroup", 1, null, "DebitPositive", true, true, "Cash, bank, debtors, inventory, GST credits and short-term assets.", "SCH-BS-01", "Asset accounts classified current."),
                Node("NonCurrentAssets", "Non-current assets", "AccountGroup", 2, null, "DebitPositive", false, true, "Fixed assets, long-term advances and accumulated capital assets.", "SCH-BS-02", "Asset accounts classified non-current."),
                Node("TotalAssets", "Total assets", "Formula", 3, "CurrentAssets+NonCurrentAssets", "DebitPositive", true, false, "Total resources controlled by the business.", "SCH-BS-03", "Formula node."),
                Node("CurrentLiabilities", "Current liabilities", "AccountGroup", 4, null, "CreditPositive", true, true, "Payables, GST/TDS, salary payable and short-term obligations.", "SCH-BS-04", "Liability accounts classified current."),
                Node("NonCurrentLiabilities", "Non-current liabilities", "AccountGroup", 5, null, "CreditPositive", false, true, "Long-term loans and non-current obligations.", "SCH-BS-05", "Liability accounts classified non-current."),
                Node("CapitalEquity", EquityLabel(normalized), "AccountGroup", 6, null, "CreditPositive", true, true, "Owner capital, partner capital or company equity.", "SCH-BS-06", "Equity accounts."),
                Node("CurrentYearProfit", "Current-year profit", "Formula", 7, null, "CreditPositive", true, false, "Profit after tax transferred from the P&L for presentation.", "SCH-BS-07", "P&L transfer node."),
                Node("TotalEquityLiabilities", "Total equity and liabilities", "Formula", 8, "CurrentLiabilities+NonCurrentLiabilities+CapitalEquity+CurrentYearProfit", "CreditPositive", true, false, "Balance Sheet funding side.", "SCH-BS-08", "Formula node.")
            ]);
    }

    public static string NormalizeStatementView(string? view)
        => string.Equals(view, "Horizontal", StringComparison.OrdinalIgnoreCase) ? "Horizontal" : "Vertical";

    public static string NormalizeEntityType(string? entityType)
    {
        if (string.Equals(entityType, "Partnership", StringComparison.OrdinalIgnoreCase) || string.Equals(entityType, "LLP", StringComparison.OrdinalIgnoreCase))
        {
            return "Partnership";
        }

        if (string.Equals(entityType, "Company", StringComparison.OrdinalIgnoreCase))
        {
            return "Company";
        }

        return "Proprietorship";
    }

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

    public static string ClassifyBalanceSheetCategory(FinalAccountsAccount account, string? groupName)
    {
        var haystack = $"{account.Code} {account.Name} {groupName ?? string.Empty}".ToLowerInvariant();
        return account.AccountType switch
        {
            FinalAccountsAccountType.Asset or FinalAccountsAccountType.ContraAsset => IsNonCurrentAsset(haystack) ? "NonCurrentAssets" : "CurrentAssets",
            FinalAccountsAccountType.Liability or FinalAccountsAccountType.ContraLiability => IsNonCurrentLiability(haystack) ? "NonCurrentLiabilities" : "CurrentLiabilities",
            FinalAccountsAccountType.Equity => "CapitalEquity",
            _ => string.Empty
        };
    }

    public static string ClassifySchedule(string category, FinalAccountsAccount account, string? groupName)
    {
        var haystack = $"{account.Code} {account.Name} {groupName ?? string.Empty}".ToLowerInvariant();
        if (ContainsAny(haystack, "customer", "receivable", "debtor", "due from"))
        {
            return "DebtorAgeing";
        }

        if (ContainsAny(haystack, "gst", "tds", "tax"))
        {
            return "GstTds";
        }

        if (ContainsAny(haystack, "vendor", "supplier", "payable", "creditor", "salary payable", "expense payable"))
        {
            return "CreditorAgeing";
        }

        if (ContainsAny(haystack, "inventory", "stock"))
        {
            return "Inventory";
        }

        if (ContainsAny(haystack, "fixed asset", "plant", "equipment", "furniture", "computer", "depreciation"))
        {
            return "FixedAssets";
        }

        if (IsCashEquivalent(account, groupName))
        {
            return "CashBank";
        }

        if (ContainsAny(haystack, "loan", "borrow", "finance"))
        {
            return "Loans";
        }

        if (category == "CapitalEquity" || ContainsAny(haystack, "capital", "equity", "drawings", "retained"))
        {
            return "Capital";
        }

        return "NotesAttachments";
    }

    public static string ClassifyCashFlowActivity(FinalAccountsAccount account, string? groupName)
    {
        var haystack = $"{account.Code} {account.Name} {groupName ?? string.Empty}".ToLowerInvariant();
        if (IsCashEquivalent(account, groupName))
        {
            return "CashEquivalent";
        }

        if (ContainsAny(haystack, "fixed asset", "plant", "equipment", "furniture", "computer", "depreciation"))
        {
            return "Investing";
        }

        if (account.AccountType == FinalAccountsAccountType.Equity || ContainsAny(haystack, "loan", "borrow", "capital", "drawings", "equity"))
        {
            return "Financing";
        }

        return "Operating";
    }

    public static bool IsCashEquivalent(FinalAccountsAccount account, string? groupName)
    {
        var haystack = $"{account.Code} {account.Name} {groupName ?? string.Empty}".ToLowerInvariant();
        return account.AccountType is FinalAccountsAccountType.Asset or FinalAccountsAccountType.ContraAsset
               && ContainsAny(haystack, "cash", "bank", "upi", "card clearing", "wallet");
    }

    public static string AgeBucket(decimal balance)
        => balance == 0m ? "Zero" : "Unaged";

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

    private static bool IsNonCurrentAsset(string haystack)
        => ContainsAny(haystack, "fixed asset", "plant", "equipment", "furniture", "computer", "deposit", "long term", "long-term", "loan asset");

    private static bool IsNonCurrentLiability(string haystack)
        => ContainsAny(haystack, "loan", "long term", "long-term", "borrow", "debenture");

    private static string EquityLabel(string entityType)
        => entityType switch
        {
            "Partnership" => "Partners' capital",
            "Company" => "Share capital and reserves",
            _ => "Owner capital"
        };

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
