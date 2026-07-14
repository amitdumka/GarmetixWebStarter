using System.ComponentModel.DataAnnotations;
using Garmetix.Core.Models.Base;

namespace Garmetix.Core.Models.FinalAccounts;

public enum FinalAccountsCloseRunStatus
{
    Draft = 1,
    Ready = 2,
    Closed = 3,
    ReopenRequested = 4,
    Reopened = 5
}

public enum FinalAccountsCloseType
{
    Period = 1,
    Year = 2
}

public sealed class FinalAccountsCloseRun : BaseEntity
{
    [Display(Name = "Company")] public Guid? CompanyId { get; set; }
    [Display(Name = "Store Group")] public Guid? StoreGroupId { get; set; }
    [Display(Name = "Store")] public Guid? StoreId { get; set; }
    [Display(Name = "Run Number")] public string RunNumber { get; set; } = string.Empty;
    [Display(Name = "Close Type")] public FinalAccountsCloseType CloseType { get; set; } = FinalAccountsCloseType.Period;
    [Display(Name = "Status")] public FinalAccountsCloseRunStatus Status { get; set; } = FinalAccountsCloseRunStatus.Draft;
    [Display(Name = "Fiscal Year")] public Guid FiscalYearId { get; set; }
    [Display(Name = "Fiscal Period")] public Guid? FiscalPeriodId { get; set; }
    [Display(Name = "Period Start")] public DateTime PeriodStart { get; set; }
    [Display(Name = "Period End")] public DateTime PeriodEnd { get; set; }
    [Display(Name = "Close Date")] public DateTime CloseDate { get; set; } = DateTime.UtcNow.Date;
    [Display(Name = "Checklist Status")] public string ChecklistStatus { get; set; } = "Pending";
    [Display(Name = "Reconciliation Status")] public string ReconciliationStatus { get; set; } = "Pending";
    [Display(Name = "Pending Posting Count")] public int PendingPostingCount { get; set; }
    [Display(Name = "Trial Balance Status")] public string TrialBalanceStatus { get; set; } = "Pending";
    [Display(Name = "Balance Sheet Status")] public string BalanceSheetStatus { get; set; } = "Pending";
    [Display(Name = "Inventory Snapshot Total")] public decimal InventorySnapshotTotal { get; set; }
    [Display(Name = "Profit After Tax")] public decimal ProfitAfterTax { get; set; }
    [Display(Name = "Result Transfer Status")] public string CurrentYearResultTransferStatus { get; set; } = "Pending";
    [Display(Name = "Opening Journal Status")] public string OpeningJournalStatus { get; set; } = "Pending";
    [Display(Name = "Report Snapshot Status")] public string ReportSnapshotStatus { get; set; } = "Pending";
    [Display(Name = "Financial Year Lock")] public Guid? FinancialYearLockId { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
    [Display(Name = "Closed By")] public string? ClosedBy { get; set; }
    [Display(Name = "Reopen Requested At")] public DateTime? ReopenRequestedAt { get; set; }
    [Display(Name = "Reopen Requested By")] public string? ReopenRequestedBy { get; set; }
    [Display(Name = "Reopened At")] public DateTime? ReopenedAt { get; set; }
    [Display(Name = "Reopened By")] public string? ReopenedBy { get; set; }
    [Display(Name = "Reopen Reason")] public string? ReopenReason { get; set; }
    [Display(Name = "Approval Notes")] public string? ApprovalNotes { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
    [Display(Name = "Created By")] public string? CreatedBy { get; set; }
    [Display(Name = "Updated By")] public string? UpdatedBy { get; set; }
}

public sealed class FinalAccountsCloseChecklistItem : BaseEntity
{
    [Display(Name = "Close Run")] public Guid CloseRunId { get; set; }
    [Display(Name = "Key")] public string Key { get; set; } = string.Empty;
    [Display(Name = "Label")] public string Label { get; set; } = string.Empty;
    [Display(Name = "Required")] public bool Required { get; set; }
    [Display(Name = "Status")] public string Status { get; set; } = "Pending";
    [Display(Name = "Detail")] public string Detail { get; set; } = string.Empty;
    [Display(Name = "Amount")] public decimal? Amount { get; set; }
    [Display(Name = "Sort Order")] public int SortOrder { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsCloseReportSnapshot : BaseEntity
{
    [Display(Name = "Close Run")] public Guid CloseRunId { get; set; }
    [Display(Name = "Report Type")] public string ReportType { get; set; } = string.Empty;
    [Display(Name = "Report Version")] public FinalAccountsReportVersionKind ReportVersion { get; set; } = FinalAccountsReportVersionKind.Final;
    [Display(Name = "Period From")] public DateTime? PeriodFrom { get; set; }
    [Display(Name = "Period To")] public DateTime? PeriodTo { get; set; }
    [Display(Name = "Status")] public string Status { get; set; } = "Generated";
    [Display(Name = "Payload JSON")] public string PayloadJson { get; set; } = "{}";
    [Display(Name = "Payload Hash")] public string PayloadHash { get; set; } = string.Empty;
    [Display(Name = "Revision")] public int Revision { get; set; }
}

public sealed class FinalAccountsCloseBalanceSnapshot : BaseEntity
{
    [Display(Name = "Close Run")] public Guid CloseRunId { get; set; }
    [Display(Name = "Account")] public Guid AccountId { get; set; }
    [Display(Name = "Account Code")] public string AccountCode { get; set; } = string.Empty;
    [Display(Name = "Account Name")] public string AccountName { get; set; } = string.Empty;
    [Display(Name = "Account Type")] public string AccountType { get; set; } = string.Empty;
    [Display(Name = "Closing Balance")] public decimal ClosingBalance { get; set; }
    [Display(Name = "Opening Balance")] public decimal OpeningBalance { get; set; }
    [Display(Name = "Revision")] public int Revision { get; set; }
}
