using Garmetix.Core.Enums;

namespace Garmetix.Api.Billing;

public sealed record PosSaleRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string? CustomerName,
    string? CustomerMobileNumber,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    decimal PaidAmount,
    decimal BillDiscountAmount,
    IReadOnlyList<PosSaleItemRequest> Items);

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
    decimal Quantity);

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
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal Amount);

public sealed record ReceiptPaymentDto(
    DateTime OnDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber);

public sealed record CancelInvoiceRequest(string? Reason);

public sealed record CancelInvoiceResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    string InvoiceStatus,
    decimal ReversedQuantity,
    decimal ReversedAmount);
