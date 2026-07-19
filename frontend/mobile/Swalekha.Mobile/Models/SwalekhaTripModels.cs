namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaTripEndpoints.cs exactly. A Trip wraps a
// dedicated SwalekhaExpenseSheet (SheetType "Travel") - expense entries are added via the
// existing /api/swalekha/expense-sheets/{sheetId}/entries routes against Trip.SheetId.

public sealed record SwalekhaTripDto(
    Guid Id,
    Guid SheetId,
    string Name,
    string? Destination,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? Budget,
    decimal SpentTotal,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaTripPayload(
    string Name,
    string? Destination,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? Budget,
    string? Notes);
