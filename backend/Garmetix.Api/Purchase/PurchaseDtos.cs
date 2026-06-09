using Garmetix.Core.Enums;

namespace Garmetix.Api.Purchase;

public sealed record PurchaseInwardRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string VendorName,
    string? VendorMobileNumber,
    string? VendorGstin,
    string? InvoiceNumber,
    string? InwardNumber,
    decimal PaidAmount,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    decimal FrightAmount,
    IReadOnlyList<PurchaseInwardItemRequest> Items,
    DateTime? SupplierInvoiceDate = null,
    DateTime? DueDate = null);

public sealed record PurchaseInwardItemRequest(
    Guid? ProductId,
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal CostPrice,
    decimal Mrp,
    decimal DiscountAmount,
    Guid? TaxId,
    Guid? ProductCategoryId,
    Guid? ProductSubCategoryId,
    string? HsnCode = null,
    Unit? ProductUnit = null);

public sealed record PurchaseInwardResponse(
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string InwardNumber,
    Guid VendorId,
    decimal BillAmount,
    decimal PaidAmount,
    int ItemCount,
    decimal Quantity,
    IReadOnlyList<string> GstinAlerts);

public sealed record RecentPurchaseInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    string InwardNumber,
    DateTime OnDate,
    DateTime InwardDate,
    string VendorName,
    string? VendorGstin,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    decimal FrightAmount,
    int ItemCount,
    decimal Quantity,
    string InvoiceStatus,
    string PaymentMode);

public sealed record PurchaseReceiptDto(
    Guid Id,
    string InvoiceNumber,
    string InwardNumber,
    DateTime OnDate,
    DateTime InwardDate,
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string CompanyGstin,
    string StoreName,
    string VendorName,
    string? VendorGstin,
    decimal MRP,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal FreightAmount,
    decimal RoundOff,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string InvoiceStatus,
    string PaymentMode,
    IReadOnlyList<PurchaseReceiptItemDto> Items);

public sealed record PurchaseReceiptItemDto(
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal Amount);

public sealed record CancelPurchaseInvoiceRequest(string? Reason);

public sealed record CancelPurchaseInvoiceResponse(
    Guid PurchaseInvoiceId,
    string InvoiceNumber,
    string InvoiceStatus,
    decimal ReversedQuantity,
    decimal ReversedAmount);

public sealed record PurchaseLookupOptionsDto(
    IReadOnlyList<PurchaseLookupOptionDto> Categories,
    IReadOnlyList<PurchaseLookupOptionDto> SubCategories,
    IReadOnlyList<PurchaseTaxOptionDto> Taxes);

public sealed record PurchaseLookupOptionDto(Guid Id, string Name);

public sealed record PurchaseTaxOptionDto(Guid Id, string Name, decimal Rate, string TaxType);

public sealed record VendorPaymentVoucherRequest(
    decimal Amount,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    string? PaymentDetails,
    string? SlipNumber,
    string? Remarks);

public sealed record VendorPaymentVoucherResponse(
    Guid VoucherId,
    string VoucherNumber,
    Guid PurchaseInvoiceId,
    string PurchaseInvoiceNumber,
    decimal Amount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string InvoiceStatus);

public sealed record PurchasePaymentDto(
    Guid Id,
    DateTime OnDate,
    decimal Amount,
    string PaymentMode,
    string? ReferenceNumber,
    Guid? VoucherId);
