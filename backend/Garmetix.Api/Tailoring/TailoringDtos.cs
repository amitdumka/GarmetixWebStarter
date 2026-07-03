using Garmetix.Core.Enums;

namespace Garmetix.Api.Tailoring;

public sealed record TailoringServiceItemRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    string ServiceCode,
    string Name,
    TailoringServiceCategory Category,
    decimal DefaultCustomerRate,
    decimal DefaultVendorRate,
    decimal TaxRate,
    string? HSNCode,
    Guid? ProductId,
    bool Active,
    string? Remarks);

public sealed record TailoringVendorRequest(
    Guid CompanyId,
    string Name,
    string? MobileNumber,
    string? Address,
    string? City,
    string? Email,
    string? GSTIN,
    bool Active);

public sealed record TailoringVendorRateRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid VendorId,
    Guid ServiceItemId,
    decimal CustomerRate,
    decimal VendorRate,
    DateTime? EffectiveFrom,
    bool Active,
    string? Remarks);

public sealed record TailoringVendorRateDto(
    Guid Id,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid VendorId,
    string VendorName,
    Guid ServiceItemId,
    string ServiceName,
    TailoringServiceCategory Category,
    decimal CustomerRate,
    decimal VendorRate,
    DateTime? EffectiveFrom,
    bool Active,
    string? Remarks);

public sealed record TailoringOrderLineRequest(
    Guid? ServiceItemId,
    string ServiceName,
    TailoringServiceCategory Category,
    string? GarmentName,
    string? Barcode,
    decimal Quantity,
    decimal CustomerRate,
    decimal VendorRate,
    decimal DiscountAmount,
    TailoringCostResponsibility CostResponsibility,
    DateTime? ExpectedDeliveryDate,
    string? MeasurementsJson,
    string? Instructions,
    string? VendorRemarks);

public sealed record TailoringOrderRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    TailoringOrderType OrderType,
    Guid CustomerId,
    Guid? VendorId,
    Guid? SourceInvoiceId,
    Guid? SourceInvoiceItemId,
    Guid? SourceProductId,
    string? SourceProductName,
    string? SourceBarcode,
    DateTime? ExpectedDeliveryDate,
    string? MeasurementsJson,
    string? CustomerInstructions,
    string? InternalRemarks,
    IReadOnlyList<TailoringOrderLineRequest> Lines);

public sealed record TailoringOrderSummaryDto(
    Guid Id,
    string OrderNumber,
    DateTime OnDate,
    string OrderType,
    string Status,
    string CustomerName,
    string? CustomerMobileNumber,
    string? VendorName,
    DateTime? ExpectedDeliveryDate,
    DateTime? DeliveredAt,
    decimal CustomerChargeAmount,
    decimal VendorCostAmount,
    decimal InHouseExpenseAmount,
    decimal CustomerReceivedAmount,
    decimal CustomerBalanceAmount,
    decimal VendorPaidAmount,
    decimal VendorBalanceAmount,
    decimal ProfitImpactAmount,
    string? ServiceInvoiceNumber);

public sealed record TailoringDashboardDto(
    int PendingOrders,
    int DueToday,
    int Overdue,
    int ReadyForDelivery,
    int DeliveredNotInvoiced,
    decimal CustomerBalance,
    decimal VendorBalance,
    decimal InHouseExpenseImpact,
    IReadOnlyList<TailoringOrderSummaryDto> UpcomingDeliveries,
    IReadOnlyList<TailoringOrderSummaryDto> OverdueOrders);

public sealed record TailoringDeliveryRequest(
    DateTime? DeliveredAt,
    string? Remarks);

public sealed record TailoringPaymentRequest(
    DateTime OnDate,
    decimal Amount,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? Remarks);

public sealed record TailoringInvoiceRequest(
    DateTime? InvoiceDate,
    Guid? SalesmanId,
    decimal AdditionalPaidAmount,
    PaymentMode? AdditionalPaymentMode,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? Remarks);

public sealed record TailoringVendorDto(
    Guid Id,
    string Name,
    string MobileNumber,
    string VendorType,
    bool Active,
    Guid? PartyId);

public sealed record TailoringStatusUpdateRequest(
    TailoringOrderStatus Status,
    DateTime? EventDate,
    string? Remarks);

public sealed record TailoringCustomerLookupDto(
    Guid Id,
    string Name,
    string MobileNumber,
    string? Email,
    string? GSTIN,
    decimal CreditBalance);

public sealed record TailoringSourceInvoiceDto(
    Guid Id,
    string InvoiceNumber,
    DateTime OnDate,
    string? CustomerName,
    string CustomerMobileNumber,
    decimal BillAmount);

public sealed record TailoringSourceInvoiceItemDto(
    Guid Id,
    Guid InvoiceId,
    Guid ProductId,
    string? ProductName,
    string Barcode,
    decimal Quantity,
    decimal MRP,
    decimal Amount,
    decimal TaxPercentage);

public sealed record TailoringDeliveryOverviewDto(
    IReadOnlyList<TailoringOrderSummaryDto> Today,
    IReadOnlyList<TailoringOrderSummaryDto> Tomorrow,
    IReadOnlyList<TailoringOrderSummaryDto> Late,
    IReadOnlyList<TailoringOrderSummaryDto> Ready,
    IReadOnlyList<TailoringOrderSummaryDto> Completed);

public sealed record TailoringPrintLineDto(
    string ServiceName,
    string? GarmentName,
    string? Barcode,
    decimal Quantity,
    decimal CustomerRate,
    decimal DiscountAmount,
    decimal TaxRate,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal LineTotal,
    string? Instructions);

public sealed record TailoringPrintDto(
    string DocumentType,
    string CopyMode,
    string OrderNumber,
    string? ServiceInvoiceNumber,
    DateTime OnDate,
    DateTime? ExpectedDeliveryDate,
    DateTime? DeliveredAt,
    string OrderType,
    string Status,
    string CustomerName,
    string? CustomerMobileNumber,
    string? VendorName,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal BillAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    string? CustomerInstructions,
    IReadOnlyList<TailoringPrintLineDto> Lines,
    IReadOnlyList<string> Copies);
