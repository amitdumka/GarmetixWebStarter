using Garmetix.Core.Enums;

namespace Garmetix.Api.Billing;

public sealed record PosSaleRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string? CustomerName,
    string? CustomerMobileNumber,
    string? CustomerGstin,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    decimal PaidAmount,
    decimal BillDiscountAmount,
    IReadOnlyList<PosSaleItemRequest> Items,
    Guid? CustomerId = null,
    Guid? SalesmanId = null,
    IReadOnlyList<InvoicePaymentDetailRequest>? Payments = null);

public sealed record InvoicePaymentDetailRequest(
    PaymentMode PaymentMode,
    decimal Amount,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? GatewayReference,
    string? SettlementStatus,
    string? AdjustmentSourceType,
    Guid? AdjustmentSourceId);

public sealed record BillingCustomerOptionDto(
    Guid Id,
    string Name,
    string MobileNumber,
    string? Gstin,
    decimal CreditBalance,
    decimal LoyaltyPoints,
    decimal LifetimeBillAmount,
    int BillCount,
    string Label);

public sealed record BillingSalesmanOptionDto(
    Guid Id,
    string Name,
    Guid StoreId,
    bool Active);

public sealed record BillingAdjustmentOptionDto(
    Guid Id,
    string Number,
    DateTime OnDate,
    decimal Amount,
    decimal AdjustedAmount,
    decimal AvailableAmount,
    string SourceType,
    string? ReferenceNumber);

public sealed record BillingLoyaltyProgramDto(
    bool Enabled,
    decimal RedeemValuePerPoint,
    decimal EarnPointsPerRupee,
    decimal MinimumBillAmount);

public sealed record BillingOptionsDto(
    IReadOnlyList<BillingCustomerOptionDto> Customers,
    IReadOnlyList<BillingSalesmanOptionDto> Salesmen,
    BillingLoyaltyProgramDto? LoyaltyProgram);

public sealed record BillingCustomerProfileDto(
    BillingCustomerOptionDto Customer,
    IReadOnlyList<BillingAdjustmentOptionDto> CreditNotes,
    IReadOnlyList<BillingAdjustmentOptionDto> AdvanceReceipts,
    BillingLoyaltyProgramDto? LoyaltyProgram);

public sealed record PosSaleItemRequest(
    Guid ProductId,
    string Barcode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount);

public sealed record PosSaleResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal NetAmount,
    decimal TaxAmount,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    int ItemCount,
    decimal Quantity,
    IReadOnlyList<string> GstinAlerts);

public sealed record RecentInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTime OnDate,
    string CustomerName,
    string CustomerMobileNumber,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string InvoiceStatus,
    string PaymentMode);

public sealed record ReceiptDto(
    Guid Id,
    string InvoiceNumber,
    DateTime OnDate,
    string CompanyName,
    string StoreName,
    string CustomerName,
    string CustomerMobileNumber,
    decimal MRP,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    IReadOnlyList<ReceiptItemDto> Items,
    IReadOnlyList<ReceiptPaymentDto> Payments);

public sealed record ReceiptItemDto(
    Guid Id,
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal? CgstAmount,
    decimal? SgstAmount,
    decimal? IgstAmount,
    string? HsnCode,
    string? Unit,
    decimal Amount);

public sealed record ReceiptPaymentDto(
    DateTime OnDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber,
    string? GatewayReference,
    string? SettlementStatus,
    string? AdjustmentSourceType);

public sealed record CancelInvoiceRequest(string? Reason);

public sealed record CancelInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    string InvoiceStatus,
    decimal ReversedQuantity,
    decimal ReversedAmount);

public sealed record SalesReturnItemRequest(
    Guid InvoiceItemId,
    decimal Quantity);

public sealed record SalesReturnRequest(
    decimal RefundAmount,
    PaymentMode? RefundPaymentMode,
    Guid? BankAccountId,
    string? Reason,
    IReadOnlyList<SalesReturnItemRequest> Items);

public sealed record SalesReturnResponse(
    Guid ReturnInvoiceId,
    string CreditNoteNumber,
    Guid OriginalInvoiceId,
    string OriginalInvoiceNumber,
    decimal CreditAmount,
    decimal RefundedAmount,
    decimal StoreCreditAmount,
    decimal ReversedQuantity,
    string OriginalInvoiceStatus);

public sealed record ExchangeSaleItemRequest(
    Guid ProductId,
    string Barcode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount);

public sealed record SalesExchangeRequest(
    decimal AdditionalPaidAmount,
    PaymentMode? AdditionalPaymentMode,
    Guid? BankAccountId,
    string? Reason,
    IReadOnlyList<SalesReturnItemRequest> ReturnItems,
    IReadOnlyList<ExchangeSaleItemRequest> NewItems);

public sealed record SalesExchangeResponse(
    Guid ReturnInvoiceId,
    string CreditNoteNumber,
    Guid ExchangeInvoiceId,
    string ExchangeInvoiceNumber,
    decimal CreditAmount,
    decimal AppliedCreditAmount,
    decimal AdditionalPaidAmount,
    decimal NewInvoiceAmount,
    decimal RemainingStoreCreditAmount);
