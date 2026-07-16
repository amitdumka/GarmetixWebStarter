using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// A named grouping of expense entries (PersonalFin_04) - Personal/House/Medical/Gifts/Hidden
/// or any custom free-text type the Owner defines. Travel sheets are added in PersonalFin_05 as
/// a specialization of this same table (a trip is just a sheet with SheetType "Travel" plus trip
/// metadata), so this shape is deliberately kept generic rather than expense-only.
/// </summary>
public class SwalekhaExpenseSheet : BaseEntity
{
    public SwalekhaExpenseSheet()
    {
        Name = string.Empty;
        SheetType = string.Empty;
    }

    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Sheet Type")] public string SheetType { get; set; }
    [Display(Name = "Budget")] public decimal? Budget { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// One expense line against a Sheet. IsHidden lets any entry be excluded from default
/// dashboards/summaries (PersonalFin_04's "Hidden Expenses" requirement) without needing a
/// separate hidden-only sheet. Deliberately standalone in v1 - not linked to a SwalekhaAccount,
/// to keep this stage's scope to categorized tracking only; a future stage can add an optional
/// payment-account link (creating a real account Withdrawal transaction) if double-entry
/// reconciliation against Accounts Hub balances is wanted.
/// </summary>
public class SwalekhaExpenseEntry : BaseEntity
{
    public SwalekhaExpenseEntry()
    {
        Category = string.Empty;
        Narration = string.Empty;
    }

    [Display(Name = "Sheet Id")] public Guid SheetId { get; set; }
    [Display(Name = "Category")] public string Category { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Entry Date")] public DateTime EntryDate { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }
    [Display(Name = "Is Hidden")] public bool IsHidden { get; set; }
}

/// <summary>
/// An income entry (salary/rental/interest/dividend/other) - needed so cash-flow reporting
/// isn't expense-only. Standalone in v1, same reasoning as SwalekhaExpenseEntry.
/// </summary>
public class SwalekhaIncomeEntry : BaseEntity
{
    public SwalekhaIncomeEntry()
    {
        Source = string.Empty;
        Narration = string.Empty;
    }

    [Display(Name = "Source")] public string Source { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Entry Date")] public DateTime EntryDate { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }
}

/// <summary>
/// A recurring bill/subscription reminder - deliberately a lightweight reminder record (a due
/// day-of-month plus a manual "mark paid" action), not a full automated recurring-transaction
/// engine. The Owner still logs the actual payment as a normal SwalekhaExpenseEntry when paid.
/// </summary>
public class SwalekhaRecurringBill : BaseEntity
{
    public SwalekhaRecurringBill()
    {
        Name = string.Empty;
    }

    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Due Day Of Month")] public int DueDayOfMonth { get; set; }
    [Display(Name = "Category")] public string? Category { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Last Paid Date")] public DateTime? LastPaidDate { get; set; }
}
