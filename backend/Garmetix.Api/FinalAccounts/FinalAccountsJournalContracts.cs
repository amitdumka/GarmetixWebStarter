namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsJournalLineRequest(
    Guid AccountId,
    decimal Debit,
    decimal Credit,
    string? Narration);

public sealed record FinalAccountsJournalSaveRequest(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime OnDate,
    Guid? FiscalPeriodId,
    string? ReferenceNumber,
    string? Narration,
    string? SourceType,
    Guid? SourceId,
    string? IdempotencyKey,
    IReadOnlyList<FinalAccountsJournalLineRequest> Lines);

public sealed record FinalAccountsJournalPostRequest(string? IdempotencyKey);

public sealed record FinalAccountsJournalReverseRequest(DateTime? OnDate, string Reason, string? IdempotencyKey);

public sealed record FinalAccountsJournalQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    string? Status,
    string? SourceType,
    int? Page,
    int? PageSize);

public sealed record FinalAccountsJournalLineDto(
    Guid Id,
    Guid AccountId,
    string? AccountCode,
    string? AccountName,
    int LineNumber,
    decimal Debit,
    decimal Credit,
    string? Narration);

public sealed record FinalAccountsJournalEventDto(DateTime At, string Event, string? Actor, string Detail);

public sealed record FinalAccountsJournalDto(
    Guid Id,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string EntryNumber,
    DateTime OnDate,
    Guid? FiscalPeriodId,
    string Status,
    string SourceType,
    Guid? SourceId,
    string? ReferenceNumber,
    string Narration,
    string? IdempotencyKey,
    Guid? ReversalOfJournalEntryId,
    Guid? ReversalJournalEntryId,
    DateTime? PostedAt,
    string? PostedBy,
    DateTime? ReversedAt,
    string? ReversedBy,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    int Revision,
    IReadOnlyList<FinalAccountsJournalLineDto> Lines,
    IReadOnlyList<FinalAccountsJournalEventDto> Events);

public sealed record FinalAccountsJournalListRowDto(
    Guid Id,
    string EntryNumber,
    DateTime OnDate,
    string Status,
    string SourceType,
    Guid? SourceId,
    string? ReferenceNumber,
    string Narration,
    decimal TotalDebit,
    decimal TotalCredit,
    int LineCount,
    DateTime CreatedAt,
    DateTime? PostedAt,
    DateTime? ReversedAt);

public sealed record FinalAccountsJournalListResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FinalAccountsJournalListRowDto> Rows);

public sealed record FinalAccountsJournalValidationResponse(
    bool CanPost,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);
