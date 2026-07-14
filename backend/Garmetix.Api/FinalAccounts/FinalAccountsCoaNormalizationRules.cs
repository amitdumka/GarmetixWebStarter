using Garmetix.Core.Enums;
using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsCoaNormalizationRules
{
    public const string StageName = "BS17IndianCOANormalization";
    public const string PreviewEndpointPath = "/api/final-accounts/audit/coa-normalization";
    public const string BackupRequirement = "Run the deployed-host backup before remote deploy or live normalization review: npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS17IndianCOANormalization";

    private static readonly IReadOnlyList<CoaPrimaryGroupRule> Rules =
    [
        Rule("CAPITAL", "Capital Account", FinalAccountsAccountType.Equity, 96, [LedgerCategory.CapitalAccount], ["capital", "owner", "partner", "proprietor", "equity"]),
        Rule("RESERVES", "Reserves & Surplus", FinalAccountsAccountType.Equity, 88, [], ["reserve", "surplus", "retained earning"]),
        Rule("SECURED_LOANS", "Secured Loans", FinalAccountsAccountType.Liability, 96, [LedgerCategory.SecuredLoans], ["secured loan", "term loan", "vehicle loan"]),
        Rule("UNSECURED_LOANS", "Unsecured Loans", FinalAccountsAccountType.Liability, 96, [LedgerCategory.UnsecuredLoans, LedgerCategory.Loan], ["unsecured loan", "loan from", "borrow"]),
        Rule("CURRENT_LIABILITIES", "Current Liabilities", FinalAccountsAccountType.Liability, 86, [LedgerCategory.CurrentLiabilities, LedgerCategory.Creditor], ["current liability", "liabilities", "payable", "outstanding"]),
        Rule("DUTIES_TAXES", "Duties & Taxes", FinalAccountsAccountType.Liability, 98, [LedgerCategory.DutiesAndTaxes], ["gst", "cgst", "sgst", "igst", "tds", "tax", "duty", "vat"]),
        Rule("SUNDRY_CREDITORS", "Sundry Creditors", FinalAccountsAccountType.Liability, 98, [LedgerCategory.SundryCreditors, LedgerCategory.Vendor], ["vendor", "supplier", "creditor", "sundry creditor"]),
        Rule("PROVISIONS", "Provisions", FinalAccountsAccountType.Liability, 84, [], ["provision", "accrual", "accrued"]),
        Rule("FIXED_ASSETS", "Fixed Assets", FinalAccountsAccountType.Asset, 96, [LedgerCategory.FixedAssets], ["fixed asset", "furniture", "computer", "machinery", "equipment", "vehicle"]),
        Rule("INVESTMENTS", "Investments", FinalAccountsAccountType.Asset, 84, [], ["investment", "fd", "fixed deposit", "mutual fund"]),
        Rule("CURRENT_ASSETS", "Current Assets", FinalAccountsAccountType.Asset, 86, [LedgerCategory.CurrentAssets, LedgerCategory.Assets], ["current asset", "asset", "advance", "deposit"]),
        Rule("BANK_ACCOUNTS", "Bank Accounts", FinalAccountsAccountType.Asset, 98, [LedgerCategory.BankAccounts, LedgerCategory.Bank], ["bank", "hdfc", "icici", "sbi", "axis", "kotak", "yes bank", "neft", "rtgs", "upi clearing", "card clearing"]),
        Rule("CASH_IN_HAND", "Cash-in-Hand", FinalAccountsAccountType.Asset, 98, [LedgerCategory.CashInHand], ["cash", "petty cash", "cash in hand"]),
        Rule("SUNDRY_DEBTORS", "Sundry Debtors", FinalAccountsAccountType.Asset, 98, [LedgerCategory.SundryDebtors, LedgerCategory.Customer, LedgerCategory.Debitor], ["customer", "debtor", "receivable", "sundry debtor"]),
        Rule("STOCK_IN_HAND", "Stock-in-Hand", FinalAccountsAccountType.Asset, 96, [LedgerCategory.Stock], ["stock", "inventory", "closing stock", "stock in hand"]),
        Rule("SALES_ACCOUNTS", "Sales Accounts", FinalAccountsAccountType.Income, 98, [LedgerCategory.SalesAccounts, LedgerCategory.Sale], ["sale", "sales", "revenue"]),
        Rule("PURCHASE_ACCOUNTS", "Purchase Accounts", FinalAccountsAccountType.Expense, 98, [LedgerCategory.PurchaseAccounts, LedgerCategory.Purchase], ["purchase", "purchases", "inward"]),
        Rule("DIRECT_INCOME", "Direct Incomes", FinalAccountsAccountType.Income, 94, [LedgerCategory.DirectIncome], ["direct income", "service income", "tailoring income"]),
        Rule("INDIRECT_INCOME", "Indirect Incomes", FinalAccountsAccountType.Income, 92, [LedgerCategory.IndirectIncome, LedgerCategory.Income], ["indirect income", "other income", "discount received", "rounding gain"]),
        Rule("DIRECT_EXPENSES", "Direct Expenses", FinalAccountsAccountType.Expense, 94, [LedgerCategory.DirectExpenses, LedgerCategory.Expenses], ["direct expense", "freight", "cartage", "wages", "cost"]),
        Rule("INDIRECT_EXPENSES", "Indirect Expenses", FinalAccountsAccountType.Expense, 92, [LedgerCategory.IndirectExpenses], ["indirect expense", "salary", "rent", "electricity", "office", "admin", "rounding loss"]),
        Rule("SUSPENSE", "Suspense Account", FinalAccountsAccountType.Liability, 70, [LedgerCategory.SuspenseAccount, LedgerCategory.UnCategory], ["suspense", "unclassified", "uncategory"])
    ];

    public static IReadOnlyList<string> IndianPrimaryGroups { get; } = Rules.Select(item => item.PrimaryGroup).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();

    public static IReadOnlyList<FinalAccountsCoaNormalizationStepDto> MigrationPlan { get; } =
    [
        new(1, "Take backup", "Run the BS17IndianCOANormalization deployed-host backup and confirm Backupfilehistory.md has a restore row."),
        new(2, "Review preview", "Export this preview and review unmapped, low-confidence and duplicate groups with Amit/CA."),
        new(3, "Freeze mapping table", "Approve a source ledger-group to Indian primary-group mapping table before any write."),
        new(4, "Normalize groups", "Create missing Indian/Tally-style primary groups or relabel existing duplicates in a reversible migration only after approval."),
        new(5, "Relink ledgers", "Move ledgers to approved groups in small batches and keep before/after evidence."),
        new(6, "Validate statements", "Run Trial Balance, P&L and Balance Sheet tie-out after each batch."),
        new(7, "Keep Final Accounts mapping exception-only", "Do not duplicate daily ledger setup in Final Accounts after Books ledgers become canonical.")
    ];

    public static IReadOnlyList<FinalAccountsCoaNormalizationStepDto> RollbackPlan { get; } =
    [
        new(1, "Stop writes", "Disable normalization/backfill job and stop any in-progress migration batch."),
        new(2, "Restore from backup if needed", "Use the BS17 backup dump when ledger-group rewrites cannot be safely reversed."),
        new(3, "Reverse relinks", "If only ledger group links changed, restore saved before/after link evidence for affected ledgers."),
        new(4, "Re-run preview", "Run this preview again and compare counts, duplicates and statement controls before reopening migration.")
    ];

    public static IReadOnlyList<ControlAccountRule> ControlAccounts { get; } =
    [
        new("CUSTOMER.RECEIVABLE", "Customer Receivables", "Sundry Debtors", FinalAccountsAccountType.Asset, ["customer receivable", "sundry debtor", "debtor"]),
        new("VENDOR.PAYABLE", "Vendor Payables", "Sundry Creditors", FinalAccountsAccountType.Liability, ["vendor payable", "sundry creditor", "creditor", "supplier"]),
        new("PAYMENT.CASH", "Cash In Hand", "Cash-in-Hand", FinalAccountsAccountType.Asset, ["cash in hand", "cash", "petty cash"]),
        new("PAYMENT.BANK", "Bank Account", "Bank Accounts", FinalAccountsAccountType.Asset, ["bank", "bank account"]),
        new("GST.OUTPUT_CGST", "Output CGST", "Duties & Taxes", FinalAccountsAccountType.Liability, ["output cgst", "cgst payable"]),
        new("GST.OUTPUT_SGST", "Output SGST", "Duties & Taxes", FinalAccountsAccountType.Liability, ["output sgst", "sgst payable"]),
        new("GST.OUTPUT_IGST", "Output IGST", "Duties & Taxes", FinalAccountsAccountType.Liability, ["output igst", "igst payable"]),
        new("GST.INPUT_CGST", "Input CGST", "Duties & Taxes", FinalAccountsAccountType.Asset, ["input cgst", "cgst input"]),
        new("GST.INPUT_SGST", "Input SGST", "Duties & Taxes", FinalAccountsAccountType.Asset, ["input sgst", "sgst input"]),
        new("GST.INPUT_IGST", "Input IGST", "Duties & Taxes", FinalAccountsAccountType.Asset, ["input igst", "igst input"]),
        new("INVENTORY.STOCK", "Inventory Stock", "Stock-in-Hand", FinalAccountsAccountType.Asset, ["inventory stock", "stock in hand", "closing stock"]),
        new("PAYROLL.PAYABLE", "Salary Payable", "Current Liabilities", FinalAccountsAccountType.Liability, ["salary payable", "payroll payable"]),
        new("PAYROLL.EXPENSE", "Payroll Expense", "Indirect Expenses", FinalAccountsAccountType.Expense, ["salary", "payroll expense", "wages"]),
        new("CAPITAL.OWNER", "Owner Capital", "Capital Account", FinalAccountsAccountType.Equity, ["capital", "owner capital", "proprietor capital"])
    ];

    public static CoaClassificationResult ClassifyLedgerGroup(string name, LedgerCategory category)
    {
        var normalized = NormalizeName(name);
        var categoryMatches = Rules.Where(rule => rule.Categories.Contains(category)).ToList();
        var keywordMatches = Rules
            .Select(rule => new
            {
                Rule = rule,
                Matched = rule.Keywords.Any(keyword => normalized.Contains(NormalizeName(keyword), StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Matched)
            .Select(item => item.Rule)
            .ToList();

        var selected = keywordMatches.OrderByDescending(item => item.Confidence).FirstOrDefault()
            ?? categoryMatches.OrderByDescending(item => item.Confidence).FirstOrDefault();

        if (selected is null)
        {
            return new("Unmapped", "Unmapped", FinalAccountsAccountType.Liability, FinalAccountsNaturalBalance.Credit, 0, "UNMAPPED");
        }

        var confidence = selected.Confidence;
        if (keywordMatches.Count == 0 && categoryMatches.Count > 0)
        {
            confidence -= 8;
        }

        return new(selected.RuleCode, selected.PrimaryGroup, selected.AccountType, FinalAccountsCatalogRules.ExpectedNaturalBalance(selected.AccountType), Math.Max(0, confidence), "AUTO_RULE");
    }

    public static string NormalizeName(string? value)
        => string.Join(" ", (value ?? string.Empty).Trim().ToUpperInvariant().Split([' ', '_', '-', '.', '/', '\\', '&'], StringSplitOptions.RemoveEmptyEntries));

    public static string SuggestedActionForConfidence(int confidence)
        => confidence >= 90 ? "Auto-suggest only; still require CA approval before write."
            : confidence >= 70 ? "Review with CA before accepting this classification."
            : "Manual classification required before any normalization migration.";

    public static bool IsReadOnlyPreviewEndpoint(string path)
        => string.Equals(path, PreviewEndpointPath, StringComparison.OrdinalIgnoreCase);

    private static CoaPrimaryGroupRule Rule(
        string code,
        string primaryGroup,
        FinalAccountsAccountType accountType,
        int confidence,
        IReadOnlyList<LedgerCategory> categories,
        IReadOnlyList<string> keywords)
        => new(code, primaryGroup, accountType, confidence, categories, keywords);

    private sealed record CoaPrimaryGroupRule(
        string RuleCode,
        string PrimaryGroup,
        FinalAccountsAccountType AccountType,
        int Confidence,
        IReadOnlyList<LedgerCategory> Categories,
        IReadOnlyList<string> Keywords);
}

public sealed record CoaClassificationResult(
    string RuleCode,
    string PrimaryGroup,
    FinalAccountsAccountType AccountType,
    FinalAccountsNaturalBalance NaturalBalance,
    int Confidence,
    string MatchSource);

public sealed record ControlAccountRule(
    string MappingKey,
    string RecommendedLedgerName,
    string PrimaryGroup,
    FinalAccountsAccountType AccountType,
    IReadOnlyList<string> SearchTerms);
