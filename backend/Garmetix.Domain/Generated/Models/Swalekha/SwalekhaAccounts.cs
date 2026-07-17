using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

public enum SwalekhaAccountType
{
    Bank,
    Cash,
    CreditCard
}

public enum SwalekhaTransactionType
{
    Deposit,
    Withdrawal,
    TransferIn,
    TransferOut
}

/// <summary>
/// A single financial account in the Swalekha Accounts Hub (PersonalFin_02) - bank, cash, or
/// credit card. SwalekhaOwnedEntity.OwnerId (PersonalFin_06) scopes it to exactly one Owner
/// login; no Company/Store scoping. CurrentBalance is the live source of truth, updated transactionally alongside every
/// SwalekhaAccountTransaction insert/delete - not recomputed from history on every read.
/// </summary>
public class SwalekhaAccount : SwalekhaOwnedEntity
{
    public SwalekhaAccount()
    {
        Name = string.Empty;
        Currency = "INR";
    }

    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Account Type")] public SwalekhaAccountType AccountType { get; set; }
    [Display(Name = "Bank Name")] public string? BankName { get; set; }
    [Display(Name = "Account Number (masked)")] public string? AccountNumberMasked { get; set; }
    [Display(Name = "IFSC")] public string? Ifsc { get; set; }
    [Display(Name = "Credit Limit")] public decimal? CreditLimit { get; set; }
    [Display(Name = "Statement Day Of Month")] public int? StatementDayOfMonth { get; set; }
    [Display(Name = "Due Day Of Month")] public int? DueDayOfMonth { get; set; }
    [Display(Name = "Opening Balance")] public decimal OpeningBalance { get; set; }
    [Display(Name = "Current Balance")] public decimal CurrentBalance { get; set; }
    [Display(Name = "Currency")] public string Currency { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Notes")] public string? Notes { get; set; }
}

/// <summary>
/// One ledger row against a SwalekhaAccount - a deposit, withdrawal, or one leg of a transfer
/// (TransferOut on the source account, TransferIn on the destination account, both legs sharing
/// TransferGroupId so they can be found and reversed together). RunningBalance is an informational
/// point-in-time snapshot captured at insert time, not a guaranteed-accurate recomputed ledger -
/// CurrentBalance on SwalekhaAccount is always the authoritative live balance.
/// </summary>
public class SwalekhaAccountTransaction : SwalekhaOwnedEntity
{
    public SwalekhaAccountTransaction()
    {
        Narration = string.Empty;
    }

    [Display(Name = "Account Id")] public Guid AccountId { get; set; }
    [Display(Name = "Transaction Type")] public SwalekhaTransactionType TransactionType { get; set; }
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Transaction Date")] public DateTime TransactionDate { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; }
    [Display(Name = "Counter Account Id")] public Guid? CounterAccountId { get; set; }
    [Display(Name = "Running Balance")] public decimal RunningBalance { get; set; }
    [Display(Name = "Transfer Group Id")] public Guid? TransferGroupId { get; set; }
}
