namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaDashboardEndpoints.cs,
// SwalekhaCalendarEndpoints.cs and SwalekhaAccountsEndpoints.cs exactly.

public sealed record SwalekhaBreakdownRow(string Category, decimal Value, string? Note);

public sealed record SwalekhaCalendarEventDto(
    DateTime Date,
    string EventType,
    string Title,
    string? Description,
    decimal? Amount,
    Guid? SourceId);

public sealed record SwalekhaAppointmentDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    bool IsAllDay,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaDashboardDto(
    decimal NetWorth,
    decimal TotalAssets,
    decimal TotalLiabilities,
    IReadOnlyList<SwalekhaBreakdownRow> AssetsBreakdown,
    IReadOnlyList<SwalekhaBreakdownRow> LiabilitiesBreakdown,
    IReadOnlyList<SwalekhaCalendarEventDto> UpcomingDues,
    IReadOnlyList<SwalekhaAppointmentDto> TodayAppointments);

public sealed record SwalekhaAccountDto(
    Guid Id,
    string Name,
    string AccountType,
    string? BankName,
    string? AccountNumberMasked,
    string? Ifsc,
    decimal? CreditLimit,
    int? StatementDayOfMonth,
    int? DueDayOfMonth,
    decimal OpeningBalance,
    decimal CurrentBalance,
    string Currency,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);
