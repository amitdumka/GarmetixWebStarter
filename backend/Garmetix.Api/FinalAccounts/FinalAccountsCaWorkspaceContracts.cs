namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsAdjustmentQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? Status,
    DateTime? From,
    DateTime? To,
    int? Page,
    int? PageSize);

public sealed record FinalAccountsAdjustmentLineRequest(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Narration,
    string? StatementLineKey);

public sealed record FinalAccountsAdjustmentSaveRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string Title,
    string? Description,
    DateTime AdjustmentDate,
    Guid? FiscalPeriodId,
    string? ReferenceNumber,
    bool AutoReverse,
    DateTime? AutoReverseDate,
    IReadOnlyList<FinalAccountsAdjustmentLineRequest> Lines);

public sealed record FinalAccountsAdjustmentWorkflowRequest(string? Notes);

public sealed record FinalAccountsAdjustmentPostRequest(string? IdempotencyKey);

public sealed record FinalAccountsAdjustmentReverseRequest(DateTime? OnDate, string Reason, string? IdempotencyKey);

public sealed record FinalAccountsAdjustmentCommentRequest(string Body, string? Visibility);

public sealed record FinalAccountsAdjustmentAttachmentRequest(string FileName, string? ContentType, string StorageReference, string? Notes);

public sealed record FinalAccountsStatementLineCommentRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string StatementType,
    string StatementLineKey,
    string ReportVersion,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string Body);

public sealed record FinalAccountsReportVersionRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string ReportType,
    string VersionKind,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string? Notes);

public sealed record FinalAccountsAdjustmentListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FinalAccountsAdjustmentListRowDto> Rows);

public sealed record FinalAccountsAdjustmentListRowDto(
    Guid Id,
    string BatchNumber,
    string Title,
    DateTime AdjustmentDate,
    string Status,
    string ReportVersion,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    bool AutoReverse,
    DateTime? AutoReverseDate,
    Guid? JournalEntryId,
    Guid? ReversalJournalEntryId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record FinalAccountsAdjustmentDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string BatchNumber,
    string Title,
    string? Description,
    DateTime AdjustmentDate,
    Guid? FiscalPeriodId,
    string Status,
    string ReportVersion,
    string AuditStatus,
    bool AutoReverse,
    DateTime? AutoReverseDate,
    string? ReferenceNumber,
    Guid? JournalEntryId,
    Guid? ReversalJournalEntryId,
    string? DecisionNotes,
    DateTime? SubmittedAt,
    string? SubmittedBy,
    DateTime? ReviewedAt,
    string? ReviewedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? RejectedAt,
    string? RejectedBy,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? ReversedAt,
    string? ReversedBy,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    int Revision,
    IReadOnlyList<FinalAccountsAdjustmentLineDto> Lines,
    IReadOnlyList<FinalAccountsAdjustmentCommentDto> Comments,
    IReadOnlyList<FinalAccountsAdjustmentAttachmentDto> Attachments,
    IReadOnlyList<FinalAccountsAdjustmentEventDto> Events);

public sealed record FinalAccountsAdjustmentLineDto(
    Guid Id,
    Guid AccountId,
    string? AccountCode,
    string? AccountName,
    int LineNumber,
    decimal Debit,
    decimal Credit,
    string? Narration,
    string? StatementLineKey);

public sealed record FinalAccountsAdjustmentCommentDto(
    Guid Id,
    string Body,
    string Visibility,
    string? CreatedBy,
    DateTime CreatedAt);

public sealed record FinalAccountsAdjustmentAttachmentDto(
    Guid Id,
    string FileName,
    string? ContentType,
    string StorageReference,
    string? Notes,
    string? UploadedBy,
    DateTime CreatedAt);

public sealed record FinalAccountsAdjustmentEventDto(DateTime At, string Event, string? Actor, string Detail);

public sealed record FinalAccountsAdjustmentPreviewResponse(
    bool CanPost,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    string ReportVersionBefore,
    string ReportVersionAfter,
    decimal ProfitLossImpact,
    decimal BalanceSheetImpact,
    IReadOnlyList<FinalAccountsAdjustmentPreviewLineDto> Lines,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsAdjustmentPreviewLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    decimal Debit,
    decimal Credit,
    decimal NetImpact,
    string StatementImpact,
    string? StatementLineKey);

public sealed record FinalAccountsStatementLineCommentDto(
    Guid Id,
    string StatementType,
    string StatementLineKey,
    string ReportVersion,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string Body,
    string? CreatedBy,
    DateTime CreatedAt);

public sealed record FinalAccountsReportVersionDto(
    Guid Id,
    string ReportType,
    string VersionKind,
    DateTime? PeriodFrom,
    DateTime? PeriodTo,
    string Status,
    string AuditStatus,
    DateTime GeneratedAt,
    string? GeneratedBy,
    string? Notes);
