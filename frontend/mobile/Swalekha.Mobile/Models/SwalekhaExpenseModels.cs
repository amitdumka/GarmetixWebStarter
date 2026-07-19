namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaExpenseEndpoints.cs exactly.
// SheetType is free text (Personal/House/Medical/Gifts/Hidden/custom), not an enum.

public sealed record SwalekhaExpenseSheetDto(
    Guid Id,
    string Name,
    string SheetType,
    decimal? Budget,
    bool IsActive,
    string? Notes,
    decimal SpentTotal,
    DateTime CreatedAt);

public sealed record SwalekhaExpenseSheetPayload(
    string Name,
    string SheetType,
    decimal? Budget,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaExpenseEntryDto(
    Guid Id,
    Guid SheetId,
    string Category,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    bool IsHidden,
    DateTime CreatedAt);

public sealed record SwalekhaExpenseEntryPayload(
    string Category,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    bool IsHidden);

public sealed record SwalekhaExpenseEntryList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaExpenseEntryDto> Rows);
