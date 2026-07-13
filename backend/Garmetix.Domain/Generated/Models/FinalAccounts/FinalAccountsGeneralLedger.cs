using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public enum FinalAccountsJournalStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public sealed class FinalAccountsJournalEntry : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Entry Number")] public string EntryNumber { get; set; } = string.Empty;
    [Display(Name = "Date")] public DateTime OnDate { get; set; } = DateTime.UtcNow.Date;
    [Display(Name = "Fiscal Period")] public Guid? FiscalPeriodId { get; set; }
    [Display(Name = "Status")] public FinalAccountsJournalStatus Status { get; set; } = FinalAccountsJournalStatus.Draft;
    [Display(Name = "Source Type")] public string SourceType { get; set; } = "ManualAdjustment";
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Narration")] public string Narration { get; set; } = string.Empty;
    [Display(Name = "Idempotency Key")] public string? IdempotencyKey { get; set; }
    [Display(Name = "Reversal Of Journal")] public Guid? ReversalOfJournalEntryId { get; set; }
    [Display(Name = "Reversal Journal")] public Guid? ReversalJournalEntryId { get; set; }
    [Display(Name = "Posted At")] public DateTime? PostedAt { get; set; }
    [Display(Name = "Posted By")] public string? PostedBy { get; set; }
    [Display(Name = "Reversed At")] public DateTime? ReversedAt { get; set; }
    [Display(Name = "Reversed By")] public string? ReversedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsJournalLine : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Journal Entry")] public Guid JournalEntryId { get; set; }
    [Display(Name = "Account")] public Guid AccountId { get; set; }
    [Display(Name = "Line Number")] public int LineNumber { get; set; }
    [Display(Name = "Debit")] public decimal Debit { get; set; }
    [Display(Name = "Credit")] public decimal Credit { get; set; }
    [Display(Name = "Narration")] public string? Narration { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsSourcePostingLink : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Journal Entry")] public Guid JournalEntryId { get; set; }
    [Display(Name = "Source Type")] public string SourceType { get; set; } = string.Empty;
    [Display(Name = "Source Id")] public Guid SourceId { get; set; }
    [Display(Name = "Source Reference")] public string? SourceReference { get; set; }
    [Display(Name = "Source Hash")] public string? SourceHash { get; set; }
    [Display(Name = "Mapping Version")] public string? MappingVersion { get; set; }
    [Display(Name = "Idempotency Key")] public string? IdempotencyKey { get; set; }
    [Display(Name = "Posted At")] public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}
