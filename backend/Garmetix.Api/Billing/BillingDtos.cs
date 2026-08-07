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
    IReadOnlyList<InvoicePaymentDetailRequest>? Payments = null,
    Guid? OriginalInvoiceId = null,
    bool ReplacementApprovalRequested = false,
    string? ReplacementReason = null,
    string? Remarks = null);

public sealed record InvoicePaymentDetailRequest(
    PaymentMode PaymentMode,
    decimal Amount,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? GatewayReference,
    string? SettlementStatus,
    string? AdjustmentSourceType,
    Guid? AdjustmentSourceId,
    string? CardLastFour = null,
    string? CardAuthorizationCode = null,
    string? CardNetwork = null,
    string? UpiVpa = null,
    string? WalletProvider = null,
    string? BankReferenceNumber = null,
    string? ChequeNumber = null,
    DateTime? ChequeDate = null,
    string? DrawerBankName = null,
    string? AccountReference = null);

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
    IReadOnlyList<string> GstinAlerts,
    string? DigitalBillPublicPath = null,
    string? DigitalBillPublicToken = null,
    string? DigitalBillWhatsAppStatus = null,
    string? DigitalBillWhatsAppMessage = null);

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
    string PaymentMode,
    Guid? DigitalBillId = null,
    string? DigitalBillPublicPath = null,
    string? DigitalBillPublicToken = null,
    bool? DigitalBillIsActive = null,
    string? DigitalBillWhatsAppStatus = null,
    int DigitalBillOpenCount = 0,
    int DigitalBillPdfDownloadCount = 0,
    int DigitalBillReviewClickCount = 0,
    DateTime? DigitalBillLastWhatsAppSentAt = null,
    string? Remarks = null);

public sealed record PagedSaleInvoicesDto(
    IReadOnlyList<RecentInvoiceDto> Items,
    int Total,
    int Page,
    int PageSize,
    string DatePreset,
    DateTime FromDate,
    DateTime ToDate,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    int CancelledCount);

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
    IReadOnlyList<ReceiptPaymentDto> Payments,
    Guid? DigitalBillId = null,
    string? DigitalBillPublicPath = null,
    string? DigitalBillPublicToken = null,
    bool? DigitalBillIsActive = null,
    string? DigitalBillWhatsAppStatus = null,
    int DigitalBillOpenCount = 0,
    int DigitalBillPdfDownloadCount = 0,
    int DigitalBillReviewClickCount = 0,
    DateTime? DigitalBillLastWhatsAppSentAt = null,
    string? Remarks = null);

public sealed record ReceiptItemDto(
    Guid Id,
    Guid? ProductId,
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
    Guid Id,
    DateTime OnDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber,
    string? GatewayReference,
    string? SettlementStatus,
    string? AdjustmentSourceType);


public sealed record UpdateSaleInvoiceRequest(
    string? InvoiceNumber,
    DateTime? OnDate,
    string? CustomerName,
    string? CustomerMobileNumber,
    string? CustomerGstin,
    Guid? SalesmanId,
    string? Remarks = null);

public sealed record RoundOffCorrectionRequest(decimal Amount, string? Note);

public sealed record RoundOffCorrectionResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    decimal PreviousRoundOff,
    decimal NewRoundOff,
    decimal PreviousBillAmount,
    decimal NewBillAmount,
    decimal PreviousPaidAmount,
    decimal NewPaidAmount,
    Guid CorrectedPaymentId,
    string CorrectedPaymentMode,
    decimal PreviousPaymentAmount,
    decimal NewPaymentAmount);

public sealed record ReclassifyInvoicePaymentRequest(string NewPaymentMode, string? ReferenceNumber, string? Note);

public sealed record ReclassifyInvoicePaymentResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid PaymentId,
    string PreviousPaymentMode,
    string NewPaymentMode,
    decimal Amount);

public sealed record CancelInvoiceRequest(string? Reason);

public sealed record CancelInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    string InvoiceStatus,
    decimal ReversedQuantity,
    decimal ReversedAmount);

public sealed record AdminHardDeleteSaleResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    string Status,
    int RemovedInvoiceItems,
    int RemovedInvoicePayments,
    int RemovedCardPayments,
    int RemovedStockMovements,
    int RemovedJournalEntries,
    int RemovedJournalLines,
    int RemovedBankTransactions,
    int RemovedBankStatementLines,
    int RemovedChequeLogs,
    int RemovedCommercialNotes,
    int RemovedLoyaltyLedgers,
    int RemovedAuditEntries);

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
