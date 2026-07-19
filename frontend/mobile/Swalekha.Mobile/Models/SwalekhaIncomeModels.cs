namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaIncomeEndpoints.cs and
// SwalekhaRecurringBillEndpoints.cs exactly.

public sealed record SwalekhaIncomeEntryDto(
    Guid Id,
    string Source,
    decimal Amount,
    DateTime EntryDate,
    string Narration,
    DateTime CreatedAt);

public sealed record SwalekhaIncomeEntryPayload(
    string Source,
    decimal Amount,
    DateTime EntryDate,
    string Narration);

public sealed record SwalekhaIncomeList(int Page, int PageSize, int TotalCount, decimal TotalAmount, IReadOnlyList<SwalekhaIncomeEntryDto> Rows);

public sealed record SwalekhaRecurringBillDto(
    Guid Id,
    string Name,
    decimal Amount,
    int DueDayOfMonth,
    string? Category,
    bool IsActive,
    string? Notes,
    DateTime? LastPaidDate,
    bool DueThisMonth);

public sealed record SwalekhaRecurringBillPayload(
    string Name,
    decimal Amount,
    int DueDayOfMonth,
    string? Category,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaMarkPaidPayload(DateTime? PaidDate);
