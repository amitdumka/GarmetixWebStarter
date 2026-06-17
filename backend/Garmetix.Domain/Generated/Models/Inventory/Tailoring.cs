using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Inventory;

/// <summary>
/// Reusable service definition used in tailoring orders, alteration orders, and service invoices.
/// </summary>
public class TailoringServiceItem : StoreBase
{
    [Display(Name = "Service Code")] public string ServiceCode { get; set; } = string.Empty;
    [Display(Name = "Service Name")] public string Name { get; set; } = string.Empty;
    [Display(Name = "Category")] public TailoringServiceCategory Category { get; set; } = TailoringServiceCategory.Stitching;
    [Display(Name = "Default Customer Rate")] public decimal DefaultCustomerRate { get; set; }
    [Display(Name = "Default Vendor Rate")] public decimal DefaultVendorRate { get; set; }
    [Display(Name = "Tax Rate")] public decimal TaxRate { get; set; }
    [Display(Name = "HSN/SAC Code")] public string? HSNCode { get; set; }
    [Display(Name = "Linked Product", AutoGenerateField = false)] public Guid? ProductId { get; set; }
    [Display(Name = "Active")] public bool Active { get; set; } = true;
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
}

public class TailoringOrder : StoreBase
{
    [Display(Name = "Order Number")] public string OrderNumber { get; set; } = string.Empty;
    [Display(Name = "Order Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Order Type")] public TailoringOrderType OrderType { get; set; } = TailoringOrderType.Stitching;
    [Display(Name = "Status")] public TailoringOrderStatus Status { get; set; } = TailoringOrderStatus.Ordered;

    [Display(Name = "Customer", AutoGenerateField = false)] public Guid CustomerId { get; set; }
    [Display(Name = "Customer Name")] public string CustomerName { get; set; } = string.Empty;
    [Display(Name = "Customer Mobile")] public string? CustomerMobileNumber { get; set; }

    [Display(Name = "Tailoring Vendor", AutoGenerateField = false)] public Guid? VendorId { get; set; }
    [Display(Name = "Tailoring Vendor Name")] public string? VendorName { get; set; }

    [Display(Name = "Source Sales Invoice", AutoGenerateField = false)] public Guid? SourceInvoiceId { get; set; }
    [Display(Name = "Source Invoice Number")] public string? SourceInvoiceNumber { get; set; }
    [Display(Name = "Source Invoice Item", AutoGenerateField = false)] public Guid? SourceInvoiceItemId { get; set; }
    [Display(Name = "Source Product", AutoGenerateField = false)] public Guid? SourceProductId { get; set; }
    [Display(Name = "Source Product Name")] public string? SourceProductName { get; set; }
    [Display(Name = "Source Barcode")] public string? SourceBarcode { get; set; }

    [Display(Name = "Expected Delivery")] public DateTime? ExpectedDeliveryDate { get; set; }
    [Display(Name = "Delivered At")] public DateTime? DeliveredAt { get; set; }
    [Display(Name = "Measurement JSON")] public string? MeasurementsJson { get; set; }
    [Display(Name = "Customer Instructions")] public string? CustomerInstructions { get; set; }
    [Display(Name = "Internal Remarks")] public string? InternalRemarks { get; set; }

    [Display(Name = "Customer Charge")] public decimal CustomerChargeAmount { get; set; }
    [Display(Name = "Vendor Cost")] public decimal VendorCostAmount { get; set; }
    [Display(Name = "In-House Expense")] public decimal InHouseExpenseAmount { get; set; }
    [Display(Name = "Paid by Customer")] public decimal CustomerReceivedAmount { get; set; }
    [Display(Name = "Paid to Vendor")] public decimal VendorPaidAmount { get; set; }
    [Display(Name = "Balance from Customer")] public decimal CustomerBalanceAmount { get; set; }
    [Display(Name = "Vendor Balance")] public decimal VendorBalanceAmount { get; set; }
    [Display(Name = "Profit Impact")] public decimal ProfitImpactAmount { get; set; }

    [Display(Name = "Service Invoice", AutoGenerateField = false)] public Guid? ServiceInvoiceId { get; set; }
    [Display(Name = "Service Invoice Number")] public string? ServiceInvoiceNumber { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }

    [JsonIgnore] public virtual ICollection<TailoringOrderLine> Lines { get; set; } = new List<TailoringOrderLine>();
    [JsonIgnore] public virtual ICollection<TailoringOrderHistory> History { get; set; } = new List<TailoringOrderHistory>();
}

public class TailoringOrderLine : CompanyBase
{
    [Display(Name = "Order", AutoGenerateField = false)] public Guid TailoringOrderId { get; set; }
    [Display(Name = "Service Item", AutoGenerateField = false)] public Guid? ServiceItemId { get; set; }
    [Display(Name = "Service Name")] public string ServiceName { get; set; } = string.Empty;
    [Display(Name = "Category")] public TailoringServiceCategory Category { get; set; } = TailoringServiceCategory.Stitching;
    [Display(Name = "Garment / Product Name")] public string? GarmentName { get; set; }
    [Display(Name = "Barcode")] public string? Barcode { get; set; }
    [Display(Name = "Quantity")] public decimal Quantity { get; set; } = 1;
    [Display(Name = "Customer Rate")] public decimal CustomerRate { get; set; }
    [Display(Name = "Vendor Rate")] public decimal VendorRate { get; set; }
    [Display(Name = "Discount Amount")] public decimal DiscountAmount { get; set; }
    [Display(Name = "Customer Charge Amount")] public decimal CustomerChargeAmount { get; set; }
    [Display(Name = "Vendor Cost Amount")] public decimal VendorCostAmount { get; set; }
    [Display(Name = "Cost Responsibility")] public TailoringCostResponsibility CostResponsibility { get; set; } = TailoringCostResponsibility.CustomerChargeable;
    [Display(Name = "Expected Delivery")] public DateTime? ExpectedDeliveryDate { get; set; }
    [Display(Name = "Delivered At")] public DateTime? DeliveredAt { get; set; }
    [Display(Name = "Status")] public TailoringOrderStatus Status { get; set; } = TailoringOrderStatus.Ordered;
    [Display(Name = "Measurements JSON")] public string? MeasurementsJson { get; set; }
    [Display(Name = "Instructions")] public string? Instructions { get; set; }
    [Display(Name = "Vendor Remarks")] public string? VendorRemarks { get; set; }

    [JsonIgnore] public virtual TailoringOrder? TailoringOrder { get; set; }
    [JsonIgnore] public virtual TailoringServiceItem? ServiceItem { get; set; }
}

public class TailoringCustomerReceipt : StoreBase
{
    [Display(Name = "Order", AutoGenerateField = false)] public Guid TailoringOrderId { get; set; }
    [Display(Name = "Receipt Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Payment Mode")] public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    [Display(Name = "Bank Account", AutoGenerateField = false)] public Guid? BankAccountId { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
    [Display(Name = "Sales Invoice Payment", AutoGenerateField = false)] public Guid? InvoicePaymentId { get; set; }
    [JsonIgnore] public virtual TailoringOrder? TailoringOrder { get; set; }
}

public class TailoringVendorPayment : StoreBase
{
    [Display(Name = "Order", AutoGenerateField = false)] public Guid TailoringOrderId { get; set; }
    [Display(Name = "Vendor", AutoGenerateField = false)] public Guid VendorId { get; set; }
    [Display(Name = "Payment Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Amount")] public decimal Amount { get; set; }
    [Display(Name = "Payment Mode")] public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    [Display(Name = "Bank Account", AutoGenerateField = false)] public Guid? BankAccountId { get; set; }
    [Display(Name = "Reference Number")] public string? ReferenceNumber { get; set; }
    [Display(Name = "Voucher", AutoGenerateField = false)] public Guid? VoucherId { get; set; }
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
    [JsonIgnore] public virtual TailoringOrder? TailoringOrder { get; set; }
}

public class TailoringOrderHistory : CompanyBase
{
    [Display(Name = "Order", AutoGenerateField = false)] public Guid TailoringOrderId { get; set; }
    [Display(Name = "Event Date")] public DateTime EventDate { get; set; } = DateTime.Now;
    [Display(Name = "Action")] public string Action { get; set; } = string.Empty;
    [Display(Name = "From Status")] public TailoringOrderStatus? FromStatus { get; set; }
    [Display(Name = "To Status")] public TailoringOrderStatus? ToStatus { get; set; }
    [Display(Name = "Actor")] public string? Actor { get; set; }
    [Display(Name = "Remarks")] public string? Remarks { get; set; }
    [Display(Name = "Details JSON")] public string? DetailsJson { get; set; }
    [JsonIgnore] public virtual TailoringOrder? TailoringOrder { get; set; }
}
