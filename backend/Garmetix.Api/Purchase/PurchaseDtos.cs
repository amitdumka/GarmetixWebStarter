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
    DateTime? DueDate = null,
    Guid? VendorId = null);

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
    Unit? ProductUnit = null,
    ProductType? ProductType = null,
    ProductGroup? ProductGroup = null);

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
    DateTime? SupplierInvoiceDate,
    DateTime DueDate,
    Guid VendorId,
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
    DateTime? SupplierInvoiceDate,
    DateTime DueDate,
    string CompanyName,
    string CompanyAddress,
    string CompanyPhone,
    string CompanyGstin,
    string StoreName,
    Guid VendorId,
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
    IReadOnlyList<PurchaseReceiptItemDto> Items,
    IReadOnlyList<PurchasePaymentDto> Payments);

public sealed record PurchaseReceiptItemDto(
    string ProductName,
    string Barcode,
    string? HsnCode,
    string Unit,
    decimal Quantity,
    decimal Mrp,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal? CgstAmount,
    decimal? SgstAmount,
    decimal? IgstAmount,
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
    IReadOnlyList<PurchaseSubCategoryOptionDto> SubCategories,
    IReadOnlyList<PurchaseTaxOptionDto> Taxes,
    IReadOnlyList<PurchaseVendorOptionDto> Vendors,
    IReadOnlyList<PurchaseEnumOptionDto> Units,
    IReadOnlyList<PurchaseEnumOptionDto> ProductTypes,
    IReadOnlyList<PurchaseEnumOptionDto> ProductGroups);

public sealed record PurchaseLookupOptionDto(Guid Id, string Name);

public sealed record PurchaseSubCategoryOptionDto(Guid Id, string Name, Guid? CategoryId);

public sealed record PurchaseTaxOptionDto(Guid Id, string Name, decimal Rate, string TaxType);

public sealed record PurchaseVendorOptionDto(
    Guid Id,
    string Name,
    string? MobileNumber,
    string? GSTIN,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount);

public sealed record PurchaseEnumOptionDto(int Value, string Label);

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
