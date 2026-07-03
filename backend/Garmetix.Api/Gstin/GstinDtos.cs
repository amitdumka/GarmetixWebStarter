namespace Garmetix.Api.Gstin;

public sealed record GstinLookupRequest(string Gstin);

public sealed record GstinLookupResponse(
    string Gstin,
    bool IsValidFormat,
    bool IsVerified,
    string Status,
    string? LegalName,
    string? TradeName,
    string? PrincipalAddress,
    string? StateCode,
    string? TaxpayerType,
    string Source,
    DateTime? VerifiedAtUtc,
    string? Message);

public sealed record PartyGstinValidationRequest(
    string PartyType,
    string Gstin,
    string? Name,
    string? Address);

public sealed record PartyGstinValidationResponse(
    GstinLookupResponse Lookup,
    bool HasMismatch,
    IReadOnlyList<string> Alerts);

public sealed record GstinProviderStatusDto(
    bool Enabled,
    bool Ready,
    string SourceName,
    string BaseUrl,
    string UrlTemplate,
    string ApiKeyHeaderName,
    int TimeoutSeconds,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Recommendations);

public sealed record GstinProviderTestRequest(
    string Gstin,
    string? PartyName = null,
    string? Address = null);

public sealed record GstinProviderTestResponse(
    bool Success,
    string Message,
    GstinLookupResponse Lookup,
    IReadOnlyList<string> Alerts,
    DateTime CheckedAtUtc);
