using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaPersonLedgerEntryType
{
    LoanGiven,
    LoanTaken,
    RepaymentReceived,
    RepaymentPaid
}

/// <summary>
/// A personal contact (PersonalFin_03) - separate from the CRM app's business customer
/// directory. The source for Person-to-Person Ledger entries. Balance is signed from the
/// Owner's point of view: positive means the contact owes the Owner money, negative means
/// the Owner owes the contact. Plain BaseEntity, no Company/Store scoping.
/// </summary>
public class SwalekhaContact : BaseEntity
{
    public SwalekhaContact()
    {
        Name = string.Empty;
    }

    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Phone")] public string? Phone { get; set; }
    [Display(Name = "Email")] public string? Email { get; set; }
    [Display(Name = "Relationship")] public string? Relationship { get; set; }
    [Display(Name = "Balance")] public decimal Balance { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// One row in a Contact's Person-to-Person ledger - a loan given/taken or a repayment either
/// direction. RunningBalance is an informational point-in-time snapshot; SwalekhaContact.Balance
/// is always the authoritative live balance, updated transactionally alongside every insert/delete.
/// </summary>
public class SwalekhaPersonLedgerEntry : BaseEntity
{
    public SwalekhaPersonLedgerEntry()
    {
        Narration = string.Empty;
    }

    [Display(Name = "Contact Id")] public Guid ContactId { get; set; }
    [Display(Name = "Entry Type")] public SwalekhaPersonLedgerEntryType EntryType { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Entry Date")] public DateTime EntryDate { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }
    [Display(Name = "Running Balance")] public decimal RunningBalance { get; set; }
}
