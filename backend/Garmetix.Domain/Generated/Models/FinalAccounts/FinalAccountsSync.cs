using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public enum FinalAccountsSyncJobStatus
{
    Queued = 1,
    Running = 2,
    Completed = 3,
    CompletedWithErrors = 4,
    Failed = 5,
    Cancelled = 6
}

public enum FinalAccountsSyncItemStatus
{
    Pending = 1,
    SkippedExisting = 2,
    Posted = 3,
    Failed = 4,
    Drifted = 5
}

public sealed class FinalAccountsSyncJob : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Job Number")] public string JobNumber { get; set; } = string.Empty;
    [Display(Name = "Status")] public FinalAccountsSyncJobStatus Status { get; set; } = FinalAccountsSyncJobStatus.Queued;
    [Display(Name = "Mode")] public string Mode { get; set; } = "Manual";
    [Display(Name = "Dry Run")] public bool DryRun { get; set; } = true;
    [Display(Name = "Scheduled")] public bool Scheduled { get; set; }
    [Display(Name = "From")] public DateTime? From { get; set; }
    [Display(Name = "To")] public DateTime? To { get; set; }
    [Display(Name = "Modules CSV")] public string ModulesCsv { get; set; } = string.Empty;
    [Display(Name = "Idempotency Key")] public string? IdempotencyKey { get; set; }
    [Display(Name = "Stop On Error")] public bool StopOnError { get; set; }
    [Display(Name = "Max Attempts")] public int MaxAttempts { get; set; } = 3;
    [Display(Name = "Retry Delay Seconds")] public int RetryDelaySeconds { get; set; } = 60;
    [Display(Name = "Source Count")] public int SourceCount { get; set; }
    [Display(Name = "Queued Count")] public int QueuedCount { get; set; }
    [Display(Name = "Skipped Count")] public int SkippedCount { get; set; }
    [Display(Name = "Failed Count")] public int FailedCount { get; set; }
    [Display(Name = "Drift Count")] public int DriftCount { get; set; }
    [Display(Name = "Started At")] public DateTime? StartedAt { get; set; }
    [Display(Name = "Completed At")] public DateTime? CompletedAt { get; set; }
    [Display(Name = "Last Checkpoint")] public string? LastCheckpoint { get; set; }
    [Display(Name = "Error Policy")] public string ErrorPolicy { get; set; } = "Continue";
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsSyncJobItem : BaseEntity
{
    [Display(Name = "Job")] public Guid JobId { get; set; }
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Source Type")] public string SourceType { get; set; } = string.Empty;
    [Display(Name = "Source Id")] public Guid SourceId { get; set; }
    [Display(Name = "Source Reference")] public string? SourceReference { get; set; }
    [Display(Name = "Source Date")] public DateTime? SourceDate { get; set; }
    [Display(Name = "Source Amount")] public decimal SourceAmount { get; set; }
    [Display(Name = "Source Hash")] public string SourceHash { get; set; } = string.Empty;
    [Display(Name = "Status")] public FinalAccountsSyncItemStatus Status { get; set; } = FinalAccountsSyncItemStatus.Pending;
    [Display(Name = "Attempt Count")] public int AttemptCount { get; set; }
    [Display(Name = "Next Attempt At")] public DateTime? NextAttemptAt { get; set; }
    [Display(Name = "Journal Entry")] public Guid? JournalEntryId { get; set; }
    [Display(Name = "Error Code")] public string? ErrorCode { get; set; }
    [Display(Name = "Error Message")] public string? ErrorMessage { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsSyncCheckpoint : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Module")] public string Module { get; set; } = string.Empty;
    [Display(Name = "Checkpoint Key")] public string CheckpointKey { get; set; } = string.Empty;
    [Display(Name = "Last Source Date")] public DateTime? LastSourceDate { get; set; }
    [Display(Name = "Last Source Id")] public Guid? LastSourceId { get; set; }
    [Display(Name = "Last Source Hash")] public string? LastSourceHash { get; set; }
    [Display(Name = "Last Job")] public Guid? LastJobId { get; set; }
    [Display(Name = "Processed Count")] public int ProcessedCount { get; set; }
    [Display(Name = "Failed Count")] public int FailedCount { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsSyncException : BaseEntity
{
    [Display(Name = "Job")] public Guid? JobId { get; set; }
    [Display(Name = "Job Item")] public Guid? JobItemId { get; set; }
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Source Type")] public string SourceType { get; set; } = string.Empty;
    [Display(Name = "Source Id")] public Guid? SourceId { get; set; }
    [Display(Name = "Severity")] public string Severity { get; set; } = "Error";
    [Display(Name = "Category")] public string Category { get; set; } = "Posting";
    [Display(Name = "Code")] public string Code { get; set; } = string.Empty;
    [Display(Name = "Message")] public string Message { get; set; } = string.Empty;
    [Display(Name = "Details JSON")] public string? DetailsJson { get; set; }
    [Display(Name = "Resolved")] public bool Resolved { get; set; }
    [Display(Name = "Resolved At")] public DateTime? ResolvedAt { get; set; }
    [Display(Name = "Resolved By")] public string? ResolvedBy { get; set; }
    [Display(Name = "Resolution Notes")] public string? ResolutionNotes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}
