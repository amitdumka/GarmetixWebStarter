namespace Garmetix.Api.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record BootstrapAdminRequest(string Name, string UserName, string Email, string Password);

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
    string AppOperation);

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);

public sealed record BootstrapStatusResponse(bool DatabaseReady, bool HasUsers, bool HasAdmin, string Message);

public sealed record ForgotPasswordRequest(string UserNameOrEmail);

public sealed record ForgotPasswordResponse(
    string Message,
    string? ResetToken,
    string? ResetUrl,
    DateTime? ExpiresAtUtc);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
