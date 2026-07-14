namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsCloseRunQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid? FiscalYearId,
    Guid? FiscalPeriodId,
    string? Status,
    int? Page,
    int? PageSize);

public sealed record FinalAccountsClosePreviewRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid FiscalYearId,
    Guid? FiscalPeriodId,
    string CloseType,
    DateTime? CloseDate,
    bool LockPeriod,
    bool TransferCurrentYearResult,
    bool GenerateOpeningJournal);

public sealed record FinalAccountsCloseCommitRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid FiscalYearId,
    Guid? FiscalPeriodId,
    string CloseType,
    DateTime? CloseDate,
    bool LockPeriod,
    bool TransferCurrentYearResult,
    bool GenerateOpeningJournal,
    bool ConfirmAllGates,
    string? ApprovalNotes);

public sealed record FinalAccountsCloseReopenRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Reason,
    bool ConfirmReopenImpact);

public sealed record FinalAccountsCloseRunListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FinalAccountsCloseRunListRowDto> Rows);

public sealed record FinalAccountsCloseRunListRowDto(
    Guid Id,
    string RunNumber,
    string CloseType,
    string Status,
    Guid FiscalYearId,
    Guid? FiscalPeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime CloseDate,
    string ChecklistStatus,
    string TrialBalanceStatus,
    string BalanceSheetStatus,
    int PendingPostingCount,
    Guid? FinancialYearLockId,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    DateTime? ReopenedAt);

public sealed record FinalAccountsCloseRunDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string RunNumber,
    string CloseType,
    string Status,
    Guid FiscalYearId,
    Guid? FiscalPeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime CloseDate,
    string ChecklistStatus,
    string ReconciliationStatus,
    int PendingPostingCount,
    string TrialBalanceStatus,
    string BalanceSheetStatus,
    decimal InventorySnapshotTotal,
    decimal ProfitAfterTax,
    string CurrentYearResultTransferStatus,
    string OpeningJournalStatus,
    string ReportSnapshotStatus,
    Guid? FinancialYearLockId,
    string? ApprovalNotes,
    string? ReopenReason,
    DateTime? ClosedAt,
    string? ClosedBy,
    DateTime? ReopenedAt,
    string? ReopenedBy,
    int Revision,
    IReadOnlyList<FinalAccountsCloseChecklistItemDto> Checklist,
    IReadOnlyList<FinalAccountsCloseReportSnapshotDto> Reports,
    IReadOnlyList<FinalAccountsCloseBalanceSnapshotDto> Balances,
    IReadOnlyList<FinalAccountsCloseEventDto> Events);

public sealed record FinalAccountsClosePreviewResponse(
    bool CanClose,
    string Status,
    string CloseType,
    Guid FiscalYearId,
    Guid? FiscalPeriodId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime CloseDate,
    decimal TrialBalanceDifference,
    decimal BalanceSheetDifference,
    decimal InventorySnapshotTotal,
    decimal ProfitAfterTax,
    int PendingPostingCount,
    string CurrentYearResultTransferStatus,
    string OpeningJournalStatus,
    IReadOnlyList<FinalAccountsCloseChecklistItemDto> Checklist,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsCloseChecklistItemDto(
    string Key,
    string Label,
    bool Required,
    string Status,
    string Detail,
    decimal? Amount,
    int SortOrder);

public sealed record FinalAccountsCloseReportSnapshotDto(
    Guid Id,
    string ReportType,
    string ReportVersion,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string Status,
    string PayloadHash,
    DateTime CreatedAt);

public sealed record FinalAccountsCloseBalanceSnapshotDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal ClosingBalance,
    decimal OpeningBalance);

public sealed record FinalAccountsCloseEventDto(DateTime At, string Event, string? Actor, string Detail);
