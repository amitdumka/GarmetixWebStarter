using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Garmetix.Core.Models.Inventory;

/// <summary>
/// A physical Stock Audit exercise for one store, spanning a Start/End date window. Operators scan barcodes
/// against this period every day of the count; each day's scans for a barcode accumulate into their own
/// <see cref="StockAuditScan"/> row, so the period's report can show both the per-day trail and the clubbed
/// period total. Purely a counting/reporting feature - it never posts a <see cref="StockMovement"/> or otherwise
/// touches live stock; correcting live stock from a Discrepancy Report finding is a separate, deliberate action
/// left to the existing Stock Operations (Physical Count) page.
/// </summary>
public class StockAuditPeriod : StoreBase
{
    public StockAuditPeriod()
    {
        Name = string.Empty;
        Status = "Open";
    }

    [Display(Name = "Name")] public string Name { get; set; }
    [Display(Name = "Start Date")] public DateTime StartDate { get; set; }
    [Display(Name = "End Date")] public DateTime EndDate { get; set; }
    [Display(Name = "Status")] public string Status { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
    [Display(Name = "Closed At")] public DateTime? ClosedAt { get; set; }
}

/// <summary>
/// One (AuditPeriod, Barcode, ScanDate) row - a day's clubbed count for a barcode within an audit period.
/// Re-scanning the same barcode on the same date accumulates into this same row (Quantity += scanned qty,
/// ScanCount++); scanning on a different date within the same period creates a new, separate row - so the
/// period's total for a barcode is the sum of its date rows, while the date-wise trail stays intact for the
/// scan log report. Product/category/size/color/price fields are snapshotted at scan time so a later Product
/// Master edit (or deletion) never rewrites what an already-recorded count actually looked like when scanned.
/// </summary>
public class StockAuditScan : StoreBase
{
    public StockAuditScan()
    {
        Barcode = string.Empty;
        ProductName = string.Empty;
    }

    [Display(Name = "Audit Period Id")] public Guid AuditPeriodId { get; set; }
    [Display(Name = "Product Id")] public Guid? ProductId { get; set; }
    [Display(Name = "Stock Id")] public Guid? StockId { get; set; }
    [Display(Name = "Barcode")] public string Barcode { get; set; }
    [Display(Name = "Scan Date")] public DateTime ScanDate { get; set; }
    [Display(Name = "Quantity")] public decimal Quantity { get; set; }
    [Display(Name = "Scan Count")] public int ScanCount { get; set; }
    [Display(Name = "First Scanned At")] public DateTime FirstScannedAt { get; set; }
    [Display(Name = "Last Scanned At")] public DateTime LastScannedAt { get; set; }

    [Display(Name = "Product Name")] public string ProductName { get; set; }
    [Display(Name = "Category Name")] public string? CategoryName { get; set; }
    [Display(Name = "Sub Category Name")] public string? SubCategoryName { get; set; }
    [Display(Name = "Color")] public string? Color { get; set; }
    [Display(Name = "Size")] public string? Size { get; set; }
    [Display(Name = "Unit")] public string? Unit { get; set; }
    [Display(Name = "MRP")] public decimal MRP { get; set; }
    [Display(Name = "Cost Price")] public decimal CostPrice { get; set; }
}
