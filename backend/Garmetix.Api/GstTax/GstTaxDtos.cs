namespace Garmetix.Api.GstTax;

public sealed record GstDashboardDto(
    int GstinVerifiedCount,
    int InactiveGstinCount,
    int ProductsMissingHsnCount,
    int ProductsMissingGstRateCount,
    int OpenGstAuditFindingsCount,
    int SaleGstMismatchCount,
    int PurchaseGstMismatchCount,
    decimal CurrentMonthTaxableSales,
    decimal CurrentMonthOutputGst,
    decimal CurrentMonthTaxablePurchases,
    decimal CurrentMonthInputGst,
    int EInvoicePendingCount,
    int EWayBillPendingCount,
    GstProviderHealthSummaryDto ProviderHealth,
    IReadOnlyList<string> PendingStageNotes);

public sealed record GstProviderHealthSummaryDto(
    int TotalProviders,
    int EnabledProviders,
    string? PrimaryLocalMasterProviderName);

public sealed record GstProviderSummaryDto(
    Guid Id,
    string ProviderName,
    string ProviderType,
    string Environment,
    bool IsEnabled,
    int Priority,
    bool FallbackEnabled,
    IReadOnlyList<string> Features);

public sealed record GstProviderDetailDto(
    Guid Id,
    string ProviderName,
    string ProviderType,
    string Environment,
    string? BaseUrl,
    string? AuthUrl,
    bool IsEnabled,
    int Priority,
    bool FallbackEnabled,
    int TimeoutSeconds,
    int MaxRetries,
    string? Notes,
    IReadOnlyList<string> Features,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? CreatedBy,
    string? UpdatedBy);

public sealed record GstProviderSaveRequest(
    string ProviderName,
    string ProviderType,
    string Environment,
    string? BaseUrl,
    string? AuthUrl,
    bool IsEnabled,
    int Priority,
    bool FallbackEnabled,
    int TimeoutSeconds,
    int MaxRetries,
    string? Notes,
    List<string>? Features);

public sealed record GstProviderCatalogDto(
    IReadOnlyList<string> ProviderTypes,
    IReadOnlyList<string> Environments,
    IReadOnlyList<string> FeatureCodes,
    IReadOnlyList<string> CredentialKeys);

public sealed record GstCredentialEntryDto(
    string CredentialKey,
    bool HasValue,
    string? MaskedDisplayValue,
    DateTime? UpdatedAt);

public sealed record GstCredentialSaveEntry(
    string CredentialKey,
    string? Value,
    bool Clear);

public sealed record GstCredentialSaveRequest(List<GstCredentialSaveEntry> Entries);

public sealed record GstProviderTestResultDto(
    bool Success,
    string Message,
    int DurationMs,
    DateTime CheckedAtUtc);

public sealed record GstinVerifyRequest(string? Gstin, bool ForceRefresh);

public sealed record GstinLookupResultDto(
    bool Success,
    string Gstin,
    string? LegalName,
    string? TradeName,
    string? TaxpayerType,
    string? RegistrationStatus,
    string? StateCode,
    string? StateName,
    string? PrincipalAddress,
    DateTime? LastVerifiedAt,
    string? Source,
    string? ErrorMessage);

public sealed record GstinManualCacheRequest(
    string Gstin,
    string? LegalName,
    string? TradeName,
    string? TaxpayerType,
    string? RegistrationStatus,
    string? StateCode,
    string? StateName,
    string? PrincipalAddress);

public sealed record GstinCacheRowDto(
    string Gstin,
    string? LegalName,
    string? TradeName,
    string? RegistrationStatus,
    string? StateCode,
    string? StateName,
    DateTime? LastVerifiedAt,
    string? VerificationSource,
    bool? IsActive);

public static class GstTaxCatalog
{
    public static readonly IReadOnlyList<string> ProviderTypes =
    [
        "LocalMasterOnly",
        "OfficialEWayBill",
        "OfficialEInvoiceNIC",
        "ClearTax",
        "MastersIndia",
        "IRISGST",
        "GSTZen",
        "SandboxProvider",
        "GenericRestProvider",
        "CustomProvider"
    ];

    public static readonly IReadOnlyList<string> Environments = ["Sandbox", "Production"];

    public static readonly IReadOnlyList<string> FeatureCodes =
    [
        "GSTIN_LOOKUP",
        "GSTIN_SYNC",
        "HSN_LOOKUP",
        "GST_RATE_LOOKUP",
        "EINVOICE_GENERATE_IRN",
        "EINVOICE_CANCEL_IRN",
        "EINVOICE_GET_IRN",
        "EINVOICE_GET_IRN_BY_DOC",
        "EINVOICE_GET_REJECTED_IRNS",
        "EINVOICE_HEALTH",
        "EWAYBILL_GENERATE",
        "EWAYBILL_CANCEL",
        "EWAYBILL_UPDATE_PARTB",
        "EWAYBILL_GET",
        "EWAYBILL_GENERATE_BY_IRN",
        "GSTR1_EXPORT",
        "GSTR3B_SUMMARY",
        "GSTR2B_IMPORT"
    ];

    public static readonly IReadOnlyList<string> CredentialKeys =
    [
        "CLIENT_ID",
        "CLIENT_SECRET",
        "API_KEY",
        "USERNAME",
        "PASSWORD",
        "GSTIN",
        "AUTH_TOKEN",
        "SEK",
        "APP_KEY",
        "PUBLIC_KEY",
        "PRIVATE_KEY",
        "CUSTOM_HEADER_1",
        "CUSTOM_HEADER_2"
    ];
}
