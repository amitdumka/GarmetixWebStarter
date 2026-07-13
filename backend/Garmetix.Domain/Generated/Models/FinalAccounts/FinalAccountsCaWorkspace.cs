using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public enum FinalAccountsAdjustmentStatus
{
    Draft = 1,
    Submitted = 2,
    Review = 3,
    Approved = 4,
    Rejected = 5,
    Posted = 6,
    Reversed = 7
}

public enum FinalAccountsReportVersionKind
{
    Provisional = 1,
    Adjusted = 2,
    Final = 3
}

public sealed class FinalAccountsAdjustmentBatch : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Batch Number")] public string BatchNumber { get; set; } = string.Empty;
    [Display(Name = "Title")] public string Title { get; set; } = string.Empty;
    [Display(Name = "Description")] public string? Description { get; set; }
    [Display(Name = "Adjustment Date")] public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow.Date;
    [Display(Name = "Fiscal Period")] public Guid? FiscalPeriodId { get; set; }
    [Display(Name = "Status")] public FinalAccountsAdjustmentStatus Status { get; set; } = FinalAccountsAdjustmentStatus.Draft;
    [Display(Name = "Auto Reversing")] public bool AutoReverse { get; set; }
    [Display(Name = "Auto Reverse Date")] public DateTime? AutoReverseDate { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Journal Entry")] public Guid? JournalEntryId { get; set; }
    [Display(Name = "Reversal Journal Entry")] public Guid? ReversalJournalEntryId { get; set; }
    [Display(Name = "Submitted At")] public DateTime? SubmittedAt { get; set; }
    [Display(Name = "Submitted By")] public string? SubmittedBy { get; set; }
    [Display(Name = "Reviewed At")] public DateTime? ReviewedAt { get; set; }
    [Display(Name = "Reviewed By")] public string? ReviewedBy { get; set; }
    [Display(Name = "Approved At")] public DateTime? ApprovedAt { get; set; }
    [Display(Name = "Approved By")] public string? ApprovedBy { get; set; }
    [Display(Name = "Rejected At")] public DateTime? RejectedAt { get; set; }
    [Display(Name = "Rejected By")] public string? RejectedBy { get; set; }
    [Display(Name = "Posted At")] public DateTime? PostedAt { get; set; }
    [Display(Name = "Posted By")] public string? PostedBy { get; set; }
    [Display(Name = "Reversed At")] public DateTime? ReversedAt { get; set; }
    [Display(Name = "Reversed By")] public string? ReversedBy { get; set; }
    [Display(Name = "Decision Notes")] public string? DecisionNotes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsAdjustmentLine : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Adjustment Batch")] public Guid AdjustmentBatchId { get; set; }
    [Display(Name = "Account")] public Guid AccountId { get; set; }
    [Display(Name = "Line Number")] public int LineNumber { get; set; }
    [Display(Name = "Debit")] public decimal Debit { get; set; }
    [Display(Name = "Credit")] public decimal Credit { get; set; }
    [Display(Name = "Narration")] public string? Narration { get; set; }
    [Display(Name = "Statement Line Key")] public string? StatementLineKey { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsAdjustmentAttachment : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Adjustment Batch")] public Guid AdjustmentBatchId { get; set; }
    [Display(Name = "File Name")] public string FileName { get; set; } = string.Empty;
    [Display(Name = "Content Type")] public string? ContentType { get; set; }
    [Display(Name = "Storage Reference")] public string StorageReference { get; set; } = string.Empty;
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Uploaded By")] public string? UploadedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsAdjustmentComment : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Adjustment Batch")] public Guid AdjustmentBatchId { get; set; }
    [Display(Name = "Body")] public string Body { get; set; } = string.Empty;
    [Display(Name = "Visibility")] public string Visibility { get; set; } = "Internal";
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsStatementLineComment : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Statement Type")] public string StatementType { get; set; } = string.Empty;
    [Display(Name = "Statement Line Key")] public string StatementLineKey { get; set; } = string.Empty;
    [Display(Name = "Report Version")] public FinalAccountsReportVersionKind ReportVersion { get; set; } = FinalAccountsReportVersionKind.Provisional;
    [Display(Name = "Period From")] public DateTime? PeriodFrom { get; set; }
    [Display(Name = "Period To")] public DateTime? PeriodTo { get; set; }
    [Display(Name = "Body")] public string Body { get; set; } = string.Empty;
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsReportVersion : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Report Type")] public string ReportType { get; set; } = string.Empty;
    [Display(Name = "Version Kind")] public FinalAccountsReportVersionKind VersionKind { get; set; } = FinalAccountsReportVersionKind.Provisional;
    [Display(Name = "Period From")] public DateTime? PeriodFrom { get; set; }
    [Display(Name = "Period To")] public DateTime? PeriodTo { get; set; }
    [Display(Name = "Generated At")] public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    [Display(Name = "Generated By")] public string? GeneratedBy { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}
