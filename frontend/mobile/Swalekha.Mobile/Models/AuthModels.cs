namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Auth/AuthDtos.cs exactly. The API serializes with
// System.Text.Json web defaults (camelCase); PropertyNameCaseInsensitive on the
// client's JsonSerializerOptions makes that irrelevant here.

public sealed record LoginRequest(string UserName, string Password);

public sealed record AuthUserDto(
    Guid Id,
    string Name,
    string UserName,
    string Email,
    string Role,
    string UserType,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    bool Admin,
    bool IsSuperAdmin,
    bool IsActive,
    string AppOperation);

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);
