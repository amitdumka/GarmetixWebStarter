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

public sealed record GstHsnRowDto(
    Guid Id,
    string HsnCode,
    string CodeType,
    string? ChapterCode,
    string? Description,
    string? TechnicalDescription,
    string? CommonTradeDescription,
    string? DefaultUqc,
    decimal? DefaultGstRate,
    decimal? CgstRate,
    decimal? SgstRate,
    decimal? IgstRate,
    decimal? CessRate,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    string? Source,
    bool IsActive,
    int ProductMappingCount);

public sealed record GstHsnSaveRequest(
    string HsnCode,
    string CodeType,
    string? ChapterCode,
    string? Description,
    string? TechnicalDescription,
    string? CommonTradeDescription,
    string? DefaultUqc,
    decimal? DefaultGstRate,
    decimal? CgstRate,
    decimal? SgstRate,
    decimal? IgstRate,
    decimal? CessRate,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    string? Source,
    bool IsActive);

public sealed record GstHsnSearchRequest(string? Query, bool GoodsOnly, bool ServicesOnly);

public sealed record GstHsnBulkStatusRequest(List<Guid> Ids, bool IsActive);

public sealed record GstHsnImportResultDto(int RowsRead, int Created, int Updated, int Skipped, IReadOnlyList<string> Errors);

public sealed record GstRateRuleRowDto(
    Guid Id,
    string RuleName,
    string? HsnCode,
    string? ProductCategory,
    string GoodsOrService,
    decimal TaxRate,
    decimal? CgstRate,
    decimal? SgstRate,
    decimal? IgstRate,
    decimal? CessRate,
    decimal? PriceThresholdFrom,
    decimal? PriceThresholdTo,
    string? ThresholdBasis,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int Priority,
    bool IsActive,
    string? Notes);

public sealed record GstRateRuleSaveRequest(
    string RuleName,
    string? HsnCode,
    string? ProductCategory,
    string GoodsOrService,
    decimal TaxRate,
    decimal? CgstRate,
    decimal? SgstRate,
    decimal? IgstRate,
    decimal? CessRate,
    decimal? PriceThresholdFrom,
    decimal? PriceThresholdTo,
    string? ThresholdBasis,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    int Priority,
    bool IsActive,
    string? Notes);

public sealed record GstRateResolveRequest(
    string? HsnCode,
    string? ProductCategory,
    decimal BasicRateAfterDiscount,
    DateTime InvoiceDate,
    bool IsIntraState);

public sealed record GstRateResolveResult(
    bool Success,
    decimal TaxRate,
    decimal CgstRate,
    decimal SgstRate,
    decimal IgstRate,
    decimal CessRate,
    string? RuleName,
    string? Source,
    IReadOnlyList<string> Warnings);

public sealed record GstAuditRuleDto(
    Guid Id,
    string RuleCode,
    string RuleName,
    string ModuleArea,
    string Severity,
    bool IsEnabled,
    bool StrictMode,
    string? MessageTemplate);

public sealed record GstAuditRuleUpdateRequest(bool IsEnabled, bool StrictMode, string? MessageTemplate);

public sealed record GstAuditRunRequest(string? ModuleArea, DateTime? FromDate, DateTime? ToDate);

public sealed record GstAuditRunResultDto(int FindingsCreated, int EntitiesScanned, IReadOnlyList<string> Notes);

public sealed record GstAuditFindingDto(
    Guid Id,
    string RuleCode,
    string Severity,
    string ModuleArea,
    string EntityType,
    Guid? EntityId,
    string? Gstin,
    string? HsnCode,
    string Message,
    string? ExpectedValue,
    string? ActualValue,
    string Status,
    DateTime CreatedAt,
    string? ReviewedBy,
    DateTime? ReviewedAt,
    Guid? InvoiceId,
    Guid? PurchaseInvoiceId);

public sealed record GstSaleReviewResponseDto(
    DateTime FromDate,
    DateTime ToDate,
    Guid? StoreId,
    string? Search,
    int Page,
    int PageSize,
    int TotalLines,
    GstSaleReviewSummaryDto Summary,
    IReadOnlyList<GstSaleReviewInvoiceDto> Invoices,
    IReadOnlyList<GstSaleReviewLineDto> Lines);

public sealed record GstSaleReviewSummaryDto(
    int InvoiceCount,
    int LineCount,
    int MismatchCount,
    int NotConfiguredCount,
    int OpenAuditFindingCount,
    decimal TaxableAmount,
    decimal StoredTaxAmount,
    decimal ResolvedTaxAmount,
    decimal TaxDifferenceAmount);

public sealed record GstSaleReviewInvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime OnDate,
    string StoreName,
    string CustomerName,
    string CustomerMobileNumber,
    bool InterState,
    int LineCount,
    int IssueCount,
    int OpenAuditFindingCount,
    decimal StoredTaxAmount,
    decimal ResolvedTaxAmount,
    decimal TaxDifferenceAmount,
    string Status);

public sealed record GstSaleReviewLineDto(
    Guid InvoiceId,
    Guid InvoiceItemId,
    string InvoiceNumber,
    DateTime OnDate,
    string StoreName,
    string CustomerName,
    Guid ProductId,
    string ProductName,
    string Barcode,
    string? HsnCode,
    string? ProductCategory,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxableValue,
    decimal StoredTaxPercentage,
    decimal ResolvedTaxPercentage,
    decimal StoredTaxAmount,
    decimal ResolvedTaxAmount,
    decimal TaxDifferenceAmount,
    string? RateSource,
    string? RuleName,
    bool IsGarmentThreshold,
    string Status);

public sealed record GstPurchaseReviewResponseDto(
    DateTime FromDate,
    DateTime ToDate,
    Guid? StoreId,
    Guid? VendorId,
    string? Search,
    int Page,
    int PageSize,
    int TotalLines,
    GstPurchaseReviewSummaryDto Summary,
    IReadOnlyList<GstPurchaseReviewInvoiceDto> Invoices,
    IReadOnlyList<GstPurchaseReviewLineDto> Lines);

public sealed record GstPurchaseReviewSummaryDto(
    int InvoiceCount,
    int LineCount,
    int MismatchCount,
    int NotConfiguredCount,
    int OpenAuditFindingCount,
    int ItcNotEligibleCount,
    int ItcAtRiskCount,
    int ItcUnverifiedCount,
    decimal TaxableAmount,
    decimal StoredTaxAmount,
    decimal ResolvedTaxAmount,
    decimal TaxDifferenceAmount);

public sealed record GstPurchaseReviewInvoiceDto(
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string InwardNumber,
    DateTime OnDate,
    string StoreName,
    Guid VendorId,
    string VendorName,
    string? VendorGSTIN,
    bool InterState,
    int LineCount,
    int IssueCount,
    int OpenAuditFindingCount,
    decimal StoredTaxAmount,
    decimal ResolvedTaxAmount,
    decimal TaxDifferenceAmount,
    string Status,
    string ItcStatus,
    string ItcNote);

public sealed record GstPurchaseReviewLineDto(
    Guid PurchaseInvoiceId,
    Guid PurchaseInvoiceItemId,
    string InvoiceNumber,
    DateTime OnDate,
    string StoreName,
    string VendorName,
    Guid ProductId,
    string ProductName,
    string Barcode,
    string? HsnCode,
    string? ProductCategory,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxableValue,
    decimal StoredTaxPercentage,
    decimal ResolvedTaxPercentage,
    decimal StoredTaxAmount,
    decimal ResolvedTaxAmount,
    decimal TaxDifferenceAmount,
    string? RateSource,
    string? RuleName,
    bool IsGarmentThreshold,
    string Status);

public sealed record GstItcRegisterResponseDto(
    DateTime FromDate,
    DateTime ToDate,
    Guid? StoreId,
    Guid? VendorId,
    string? Search,
    int Page,
    int PageSize,
    int TotalRows,
    GstItcRegisterSummaryDto Summary,
    IReadOnlyList<GstItcRegisterRowDto> Rows);

public sealed record GstItcRegisterSummaryDto(
    int InvoiceCount,
    int EligibleCount,
    int AtRiskCount,
    int UnverifiedCount,
    int NotEligibleCount,
    decimal TotalTaxableValue,
    decimal TotalTaxAmount,
    decimal TotalEligibleItcAmount,
    decimal TotalIneligibleItcAmount);

public sealed record GstItcRegisterRowDto(
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string InwardNumber,
    DateTime OnDate,
    string VendorName,
    string? VendorGSTIN,
    bool InterState,
    decimal TaxableValue,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TaxAmount,
    string ItcStatus,
    string ItcNote,
    decimal EligibleItcAmount);

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
