namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaContactsEndpoints.cs exactly.
// Balance is signed from the Owner's point of view: positive = contact owes the Owner.

public sealed record SwalekhaContactDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    string? Relationship,
    decimal Balance,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaContactPayload(
    string Name,
    string? Phone,
    string? Email,
    string? Relationship,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaPersonLedgerEntryDto(
    Guid Id,
    Guid ContactId,
    string EntryType,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    decimal RunningBalance,
    DateTime CreatedAt);

public sealed record SwalekhaPersonLedgerEntryPayload(
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    string EntryType);

public sealed record SwalekhaPersonLedgerList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaPersonLedgerEntryDto> Rows);

public sealed record SwalekhaSettlePayload(DateTime EntryDate, string? Narration);
