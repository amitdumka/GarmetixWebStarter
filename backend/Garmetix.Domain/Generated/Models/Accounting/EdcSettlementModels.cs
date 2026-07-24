using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Accounting
{
    /// <summary>
    /// Reconciles receipts posted against a POS/EDC Machine BankAccount (AccountType.PosMachine - e.g.
    /// a PhonePe device) with the later, lump-sum bank credit the payment aggregator actually settles,
    /// net of its processing charges. Sale invoice payments are posted to the POS/EDC account's own
    /// clearing ledger on the sale date; a batch here transfers a selected set of those receipts'
    /// gross total out of that clearing ledger into the real bank account that received the money,
    /// booking the shortfall as a processing-charge expense.
    /// </summary>
    public class EdcSettlementBatch : StoreBase
    {
        [Display(Name = "POS/EDC Account", AutoGenerateField = false)] public Guid PosMachineAccountId { get; set; }
        [Display(Name = "POS/EDC Account", AutoGenerateField = false)] public virtual Accounting.BankAccount? PosMachineAccount { get; set; }
        [Display(Name = "Settled To Bank Account", AutoGenerateField = false)] public Guid RealBankAccountId { get; set; }
        [Display(Name = "Settled To Bank Account", AutoGenerateField = false)] public virtual Accounting.BankAccount? RealBankAccount { get; set; }
        [Display(Name = "Settlement Date")] public DateTime SettlementDate { get; set; } = DateTime.Now;
        [Display(Name = "Gross Amount")] public decimal GrossAmount { get; set; }
        [Display(Name = "Net Amount Received")] public decimal NetAmountReceived { get; set; }
        [Display(Name = "Charge Amount")] public decimal ChargeAmount { get; set; }
        [Display(Name = "Payment Count")] public int PaymentCount { get; set; }
        [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
        [Display(Name = "Remarks")] public string? Remarks { get; set; }
        [Display(Name = "Journal Entry", AutoGenerateField = false)] public Guid? JournalEntryId { get; set; }
        [Display(Name = "Journal Entry Number")] public string? JournalEntryNumber { get; set; }
        [Display(Name = "Reversed")] public bool Reversed { get; set; } = false;
        [Display(Name = "Reversed At")] public DateTime? ReversedAt { get; set; }
        [Display(Name = "Reversed By")] public string? ReversedBy { get; set; }
    }
}
