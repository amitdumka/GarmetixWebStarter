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
    IReadOnlyList<PurchaseInwardItemRequest> Items);

public sealed record PurchaseInwardItemRequest(
    Guid? ProductId,
    string ProductName,
    string Barcode,
    decimal Quantity,
    decimal CostPrice,
    decimal Mrp,
    decimal DiscountAmount,
    Guid? TaxId);

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
