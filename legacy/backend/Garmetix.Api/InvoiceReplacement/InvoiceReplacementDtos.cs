namespace Garmetix.Api.InvoiceReplacement;

public sealed record InvoiceReplacementPendingDto(
    string DocumentType,
    Guid RevisedInvoiceId,
    string RevisedNumber,
    DateTime RevisedDate,
    decimal RevisedAmount,
    string RevisedStatus,
    Guid OriginalInvoiceId,
    string OriginalNumber,
    DateTime OriginalDate,
    decimal OriginalAmount,
    string OriginalStatus,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string PartyName,
    bool ReadyForApproval,
    IReadOnlyList<string> Checks);

public sealed record InvoiceReplacementApprovalRequest(string? ApprovalNote);

public sealed record InvoiceReplacementApprovalResultDto(
    string DocumentType,
    Guid RevisedInvoiceId,
    string RevisedNumber,
    Guid OriginalInvoiceId,
    string OriginalNumber,
    string OriginalStatus,
    bool Approved,
    string Message);

public sealed record InvoiceReplacementAuditDto(
    Guid Id,
    DateTime OccurredAt,
    string Action,
    string EntityDisplayName,
    Guid EntityId,
    string Reference,
    string? UserName,
    string? Reason,
    string? RequestPath);
