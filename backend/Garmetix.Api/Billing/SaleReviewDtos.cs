namespace Garmetix.Api.Billing;

public sealed record SaleReviewResponseDto(
    DateTime FromDate,
    DateTime ToDate,
    Guid? StoreId,
    string? Search,
    int Page,
    int PageSize,
    int TotalItems,
    SaleReviewSummaryDto Summary,
    IReadOnlyList<SaleReviewInvoiceDto> Invoices,
    IReadOnlyList<SaleReviewItemDto> Items);

public sealed record SaleReviewSummaryDto(
    int InvoiceCount,
    int ItemCount,
    int IssueCount,
    decimal GrossBillAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal ExpectedTaxAmount,
    decimal TaxDifferenceAmount,
    decimal ExtraAmountToReview,
    decimal CostAmount,
    decimal ProfitAmount,
    decimal ProfitPercentage);

public sealed record SaleReviewInvoiceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime OnDate,
    string StoreName,
    string CustomerName,
    string CustomerMobileNumber,
    decimal BillAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal ExpectedTaxAmount,
    decimal TaxDifferenceAmount,
    decimal ExtraAmountToReview,
    decimal CostAmount,
    decimal ProfitAmount,
    decimal ProfitPercentage,
    int ItemCount,
    int IssueCount,
    string Status);

public sealed record SaleReviewItemDto(
    Guid InvoiceId,
    Guid InvoiceItemId,
    string InvoiceNumber,
    DateTime OnDate,
    string StoreName,
    string CustomerName,
    string CustomerMobileNumber,
    Guid ProductId,
    string ProductName,
    string Barcode,
    string? HsnCode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal UnitBasicPrice,
    decimal TaxableAmount,
    decimal TaxPercentage,
    decimal ExpectedTaxPercentage,
    decimal TaxAmount,
    decimal ExpectedTaxAmount,
    decimal TaxDifferenceAmount,
    decimal Amount,
    decimal ExpectedAmount,
    decimal ExtraAmountToReview,
    decimal CostRate,
    decimal CostAmount,
    decimal ProfitAmount,
    decimal ProfitPercentage,
    string Status,
    string SuggestedAction);


public sealed record SaleReviewApplyAdjustmentRequest(
    Guid InvoiceId,
    IReadOnlyList<Guid>? InvoiceItemIds,
    Guid? EmployeeId,
    string? Remarks,
    bool DryRun = false);

public sealed record SaleReviewAdjustmentResultDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime OnDate,
    int AdjustedItemCount,
    decimal OldBillAmount,
    decimal NewBillAmount,
    decimal InvoiceReductionAmount,
    decimal ExtraAmountVoucherAmount,
    Guid? ExtraAmountVoucherId,
    string? ExtraAmountVoucherNumber,
    bool DryRun,
    IReadOnlyList<SaleReviewAdjustmentItemDto> Items,
    string Message);

public sealed record SaleReviewAdjustmentItemDto(
    Guid InvoiceItemId,
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal OldTaxPercentage,
    decimal NewTaxPercentage,
    decimal OldTaxableAmount,
    decimal NewTaxableAmount,
    decimal OldTaxAmount,
    decimal NewTaxAmount,
    decimal OldLineAmount,
    decimal NewLineAmount,
    decimal ExtraAmount);
