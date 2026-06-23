using Garmetix.Core.Enums;
using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Garmetix.Core.Models.Inventory;

public class StockOperationDocument : StoreBase
{
    [Display(Name = "Document Number")] public required string DocumentNumber { get; set; } = string.Empty;
    [Display(Name = "Operation Date")] public DateTime OnDate { get; set; } = DateTime.Now;
    [Display(Name = "Operation Type")] public required string OperationType { get; set; } = string.Empty;
    [Display(Name = "Status")] public string Status { get; set; } = "Posted";
    [Display(Name = "Source Store", AutoGenerateField = false)] public Guid? FromStoreId { get; set; }
    [Display(Name = "Source Store")] public string? FromStoreName { get; set; }
    [Display(Name = "Destination Store", AutoGenerateField = false)] public Guid? ToStoreId { get; set; }
    [Display(Name = "Destination Store")] public string? ToStoreName { get; set; }
    [Display(Name = "Reason")] public string Reason { get; set; } = string.Empty;
    [Display(Name = "Total Quantity")] public decimal TotalQuantity { get; set; }
    [Display(Name = "Cost Value")] public decimal TotalCostValue { get; set; }
    [Display(Name = "MRP Value")] public decimal TotalMrpValue { get; set; }
    [Display(Name = "Item Count")] public int ItemCount { get; set; }
    [Display(Name = "Posted At")] public DateTime PostedAt { get; set; } = DateTime.Now;
    [Display(Name = "Accounting Status")] public string AccountingStatus { get; set; } = "Pending";
    [Display(Name = "Journal Entry", AutoGenerateField = false)] public Guid? JournalEntryId { get; set; }

    [JsonIgnore]
    public virtual ICollection<StockOperationItem> Items { get; set; } = new List<StockOperationItem>();
}

public class StockOperationItem : CompanyBase
{
    [Display(Name = "Stock Operation", AutoGenerateField = false)] public Guid StockOperationDocumentId { get; set; }
    [Display(Name = "Product", AutoGenerateField = false)] public Guid ProductId { get; set; }
    [Display(Name = "Stock", AutoGenerateField = false)] public Guid? StockId { get; set; }
    [Display(Name = "Destination Stock", AutoGenerateField = false)] public Guid? DestinationStockId { get; set; }
    [Display(Name = "Product Name")] public required string ProductName { get; set; } = string.Empty;
    [Display(Name = "Barcode")] public required string Barcode { get; set; } = string.Empty;
    [Display(Name = "HSN Code")] public string? HSNCode { get; set; }
    [Display(Name = "Unit")] public Unit Unit { get; set; }
    [Display(Name = "Source Store", AutoGenerateField = false)] public Guid? FromStoreId { get; set; }
    [Display(Name = "Destination Store", AutoGenerateField = false)] public Guid? ToStoreId { get; set; }
    [Display(Name = "System Quantity")] public decimal SystemQuantity { get; set; }
    [Display(Name = "Counted Quantity")] public decimal? CountedQuantity { get; set; }
    [Display(Name = "Quantity In")] public decimal QuantityIn { get; set; }
    [Display(Name = "Quantity Out")] public decimal QuantityOut { get; set; }
    [Display(Name = "Quantity Difference")] public decimal QuantityDifference { get; set; }
    [Display(Name = "Source Quantity Before")] public decimal FromQuantityBefore { get; set; }
    [Display(Name = "Source Quantity After")] public decimal FromQuantityAfter { get; set; }
    [Display(Name = "Destination Quantity Before")] public decimal? ToQuantityBefore { get; set; }
    [Display(Name = "Destination Quantity After")] public decimal? ToQuantityAfter { get; set; }
    [Display(Name = "Cost Price")] public decimal CostPrice { get; set; }
    [Display(Name = "MRP")] public decimal MRP { get; set; }
    [Display(Name = "Cost Value")] public decimal CostValue { get; set; }
    [Display(Name = "MRP Value")] public decimal MrpValue { get; set; }
    [Display(Name = "Out Movement", AutoGenerateField = false)] public Guid? OutMovementId { get; set; }
    [Display(Name = "In Movement", AutoGenerateField = false)] public Guid? InMovementId { get; set; }
    [Display(Name = "Reason")] public string? Reason { get; set; }

    [JsonIgnore] public virtual StockOperationDocument? StockOperationDocument { get; set; }
}
