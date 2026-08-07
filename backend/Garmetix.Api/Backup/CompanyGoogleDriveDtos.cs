namespace Garmetix.Api.Backup;

public sealed record CompanyDriveStatusDto(
    bool OAuthConfigured,
    bool Connected,
    string? GoogleAccountEmail,
    string? FolderName,
    DateTime? ConnectedAtUtc,
    DateTime? LastSuccessAtUtc,
    string? LastAction,
    string? LastError);

public sealed record StartOAuthResponseDto(string AuthorizationUrl);

public sealed record CompanyGoogleDriveFileDto(
    string Id,
    string Name,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string WebViewLink);
