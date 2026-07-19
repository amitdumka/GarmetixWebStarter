namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaJournalEndpoints.cs, SwalekhaNoteEndpoints.cs and
// SwalekhaCalendarEndpoints.cs exactly. SwalekhaAppointmentDto/SwalekhaCalendarEventDto already
// exist in SwalekhaModels.cs (used by the Dashboard) - only the missing payload is added here.

public sealed record SwalekhaJournalEntryDto(
    Guid Id,
    DateTime EntryDate,
    string? Title,
    string Content,
    string? Mood,
    DateTime CreatedAt);

public sealed record SwalekhaJournalEntryPayload(
    DateTime EntryDate,
    string? Title,
    string Content,
    string? Mood);

public sealed record SwalekhaPersonalNoteDto(
    Guid Id,
    string Title,
    string Content,
    string? Folder,
    string? Tags,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record SwalekhaPersonalNotePayload(
    string Title,
    string Content,
    string? Folder,
    string? Tags,
    bool IsPinned);

public sealed record SwalekhaAppointmentPayload(
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime? EndAt,
    string? Location,
    bool IsAllDay,
    string? Notes);
