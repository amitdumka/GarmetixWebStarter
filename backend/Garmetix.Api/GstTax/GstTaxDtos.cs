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
