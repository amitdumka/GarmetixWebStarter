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
