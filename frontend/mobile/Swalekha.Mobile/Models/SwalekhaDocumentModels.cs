namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaDocumentEndpoints.cs and
// SwalekhaSecurityEndpoints.cs exactly.

public sealed record SwalekhaDocumentDto(
    Guid Id,
    string EntityType,
    Guid? EntityId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaSelfCheckItem(string Name, bool Passed, string Detail);

public sealed record SwalekhaSelfCheckDto(bool AllPassed, Guid OwnerId, IReadOnlyList<SwalekhaSelfCheckItem> Checks);
