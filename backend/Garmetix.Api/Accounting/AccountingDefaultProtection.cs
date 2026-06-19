using Garmetix.Core.Models.Accounting;

namespace Garmetix.Api.Accounting;

public static class AccountingDefaultProtection
{
    public const string CreatedByMarker = "SystemDefaultAccounting";

    public static readonly ISet<string> ProtectedLedgerGroupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Capital Account",
        "Loans - Secured",
        "Loans - Unsecured",
        "Duties & Taxes",
        "Current Assets",
        "Fixed Assets",
        "Current Liabilities",
        "Sundry Debtors",
        "Sundry Creditors",
        "Bank Accounts",
        "Cash-in-Hand",
        "Direct Income",
        "Indirect Income",
        "Direct Expenses",
        "Indirect Expenses",
        "Purchase Accounts",
        "Sales Accounts",
        "Snacks & Refreshments",
        "Store Expenses",
        "Petty Expenses",
        "No Group",
        "Vendors",
        "Customers",
        "Employees",
        "Stock",
        "Debitors",
        "Creditors",
        "Banks",
        "Cash",
        "Sales",
        "Purchases"
    };

    public static readonly ISet<string> ProtectedLedgerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Dan",
        "Snacks & Tea",
        "Electricity",
        "Water",
        "Printing & Stationery",
        "Transport & Freight Charges",
        "Miscellaneous",
        "No Party",
        "Cash In Hand",
        "Salary Payables",
        "Internet & Mobile Bills",
        "Store Maintenance",
        "Store Supplies",
        "Petty Cash Expenses",
        "Sales",
        "Sales Return",
        "Purchases",
        "Purchase Return",
        "Bank Clearing",
        "Sundry Debtors Control",
        "Sundry Creditors Control",
        "State Bank of India (SBI) Current Account"
    };

    public static bool IsProtectedLedgerGroup(LedgerGroup group)
        => IsSystemDefault(group.CreatedBy) || ProtectedLedgerGroupNames.Contains(group.Name ?? string.Empty);

    public static bool IsProtectedLedger(Ledger ledger)
        => IsSystemDefault(ledger.CreatedBy) || ProtectedLedgerNames.Contains(ledger.Name ?? string.Empty);

    public static bool IsSystemDefault(string? createdBy)
        => string.Equals(createdBy, CreatedByMarker, StringComparison.OrdinalIgnoreCase);
}
