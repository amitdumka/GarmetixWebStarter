using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using Garmetix.Core.Models.HRM;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Accounting;

public class JournalEntry : StoreBase
{
    [Display(Name = "Entry Number")] public string EntryNumber { get; set; } = string.Empty;
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Source Type")] public string SourceType { get; set; } = string.Empty;
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; } = string.Empty;
    [Display(Name = "Posted")] public bool Posted { get; set; } = true;
    [Display(Name = "Posted At")] public DateTime PostedAt { get; set; } = DateTime.Now;
    [Display(Name = "Posted By")] public string? PostedBy { get; set; }

    [JsonIgnore] public virtual ICollection<JournalLine>? Lines { get; set; }
}

public class JournalLine : StoreBase
{
    [Display(Name = "Journal Entry", AutoGenerateField = false)] public Guid JournalEntryId { get; set; }
    [JsonIgnore]
    [Display(Name = "Journal Entry", AutoGenerateField = false)] public virtual JournalEntry? JournalEntry { get; set; }

    [Display(Name = "Ledger", AutoGenerateField = false)] public Guid LedgerId { get; set; }
    [JsonIgnore]
    [Display(Name = "Ledger", AutoGenerateField = false)] public virtual Ledger? Ledger { get; set; }

    [Display(Name = "Party", AutoGenerateField = false)] public Guid? PartyId { get; set; }
    [JsonIgnore]
    [Display(Name = "Party", AutoGenerateField = false)] public virtual Party? Party { get; set; }

    [Display(Name = "Employee", AutoGenerateField = false)] public Guid? EmployeeId { get; set; }
    [JsonIgnore]
    [Display(Name = "Employee", AutoGenerateField = false)] public virtual Employee? Employee { get; set; }

    [Display(Name = "Debit")] public decimal Debit { get; set; }
    [Display(Name = "Credit")] public decimal Credit { get; set; }
    [Display(Name = "Narration")] public string? Narration { get; set; }
}

public class BankStatementLine : CompanyBase
{
    [Display(Name = "Bank Account", AutoGenerateField = false)] public Guid BankAccountId { get; set; }
    [JsonIgnore]
    [Display(Name = "Bank Account", AutoGenerateField = false)] public virtual BankAccount? BankAccount { get; set; }
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Value Date")] public DateTime? ValueDate { get; set; }
    [Display(Name = "Description")] public string Description { get; set; } = string.Empty;
    [Display(Name = "Reference")] public string? Reference { get; set; }
    [Display(Name = "Debit")] public decimal Debit { get; set; }
    [Display(Name = "Credit")] public decimal Credit { get; set; }
    [Display(Name = "Balance")] public decimal Balance { get; set; }
    [Display(Name = "Reconciled")] public bool Reconciled { get; set; }
    [Display(Name = "Bank Transaction", AutoGenerateField = false)] public Guid? BankTransactionId { get; set; }
    [JsonIgnore]
    [Display(Name = "Bank Transaction", AutoGenerateField = false)] public virtual BankTransaction? BankTransaction { get; set; }
}
