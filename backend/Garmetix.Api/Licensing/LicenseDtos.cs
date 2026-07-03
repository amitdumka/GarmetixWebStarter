namespace Garmetix.Api.Licensing;

public sealed record LicenseGenerateRequest(
    string ClientCode,
    string ClientName,
    string? Plan,
    DateTimeOffset? ExpiresAtUtc,
    int? ValidityDays,
    int? MaxStores,
    int? MaxUsers,
    IReadOnlyList<string>? Modules,
    string? IssuedBy);

public sealed record LicenseActivateRequest(string LicenseKey);

public sealed record LicensePayloadDto(
    string ProductCode,
    string ClientCode,
    string ClientName,
    string Plan,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    int MaxStores,
    int MaxUsers,
    IReadOnlyList<string> Modules,
    string IssuedBy);

public sealed record LicenseActivationFileDto(
    string LicenseKey,
    string LicenseFingerprint,
    LicensePayloadDto Payload,
    DateTimeOffset ActivatedAtUtc,
    string MachineName,
    string ActivatedBy);

public sealed record LicenseGenerateResponseDto(
    string LicenseKey,
    string LicenseFingerprint,
    LicensePayloadDto Payload,
    string Message);

public sealed record LicenseActivationStatusDto(
    bool EnforcementEnabled,
    bool RequireLicenseForOperationalApis,
    bool Ready,
    bool Activated,
    bool Valid,
    string State,
    string Message,
    string ProductCode,
    string? ClientCode,
    string? ClientName,
    string? Plan,
    DateTimeOffset? IssuedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    int? DaysRemaining,
    int? MaxStores,
    int? MaxUsers,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> RequiredModules,
    DateTimeOffset? ActivatedAtUtc,
    string? MachineName,
    string? LicenseFingerprint,
    bool MasterSecretConfigured,
    string ActivationFilePath,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> RecommendedEnvironmentKeys);
