using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.GstReturns;
using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Inventory;

/// <summary>
/// Stock Audit: physical stock counting by barcode scan, per store, over a defined audit period. Every scan is
/// stored as (AuditPeriod, Barcode, ScanDate) - re-scanning the same barcode on the same day accumulates into
/// that day's row, while scanning on a different day within the same period creates a new, separate date row
/// (so the period total is the sum of its date rows, and the day-wise trail is preserved for the scan log
/// report). Read-only against live stock: this module never posts a <see cref="StockMovement"/> - it counts and
/// reports (scan log, audited-vs-current discrepancy, category/size/color stock summary from either source).
/// Correcting live stock from a discrepancy is a deliberate separate action on the existing Stock Operations
/// (Physical Count) page, not something this module does automatically.
/// </summary>
public static class StockAuditEndpoints
{
    private const int MaxRowsPerRequest = 5000;

    public static RouteGroupBuilder MapInventoryStockAuditEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/stock-audit")
            .WithTags("Inventory Stock Audit")
            .RequireAuthorization(GarmetixPolicies.StockAudit);

        group.MapGet("/periods", ListPeriodsAsync);
        group.MapGet("/periods/{id:guid}", GetPeriodAsync);
        group.MapPost("/periods", CreatePeriodAsync);
        group.MapPut("/periods/{id:guid}", UpdatePeriodAsync);
        group.MapPost("/periods/{id:guid}/close", ClosePeriodAsync);
        group.MapPost("/periods/{id:guid}/reopen", ReopenPeriodAsync);
        group.MapDelete("/periods/{id:guid}", DeletePeriodAsync).RequireAuthorization(GarmetixPolicies.Delete);

        group.MapPost("/scan", RecordScanAsync);
        group.MapDelete("/scans/{id:guid}", DeleteScanAsync);

        group.MapGet("/periods/{id:guid}/scans", ScanLogAsync);
        group.MapGet("/periods/{id:guid}/scan-summary", ScanSummaryAsync);
        group.MapGet("/periods/{id:guid}/scan-summary/csv", ScanSummaryCsvAsync);

        group.MapGet("/periods/{id:guid}/discrepancy", DiscrepancyAsync);
        group.MapGet("/periods/{id:guid}/discrepancy/csv", DiscrepancyCsvAsync);
        group.MapGet("/periods/{id:guid}/discrepancy/excel", DiscrepancyExcelAsync);

        group.MapGet("/stock-summary", StockSummaryAsync);
        group.MapGet("/stock-summary/csv", StockSummaryCsvAsync);
        group.MapGet("/stock-summary/excel", StockSummaryExcelAsync);

        return group;
    }

    // ---------------------------------------------------------------- Periods

    private static async Task<IReadOnlyList<StockAuditPeriodRowDto>> ListPeriodsAsync(
        HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, string? status, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var query = WorkspaceScope.ApplyTo(db.StockAuditPeriods.AsNoTracking().Where(p => !p.Deleted), context);
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        var periods = await query.OrderByDescending(p => p.StartDate).ThenByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
        return await ToRowsAsync(periods, db, cancellationToken);
    }

    private static async Task<IResult> GetPeriodAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.AsNoTracking().Where(p => !p.Deleted), context)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        var rows = await ToRowsAsync([period], db, cancellationToken);
        return Results.Ok(rows[0]);
    }

    private static async Task<IResult> CreatePeriodAsync(
        CreateStockAuditPeriodRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var name = Clean(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { message = "Name is required." });
        }
        if (request.EndDate.Date < request.StartDate.Date)
        {
            return Results.BadRequest(new { message = "End date cannot be before start date." });
        }

        var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context).FirstOrDefaultAsync(s => s.Id == request.StoreId, cancellationToken);
        if (store is null)
        {
            return Results.BadRequest(new { message = "Store was not found or is outside your permitted workspace." });
        }

        var period = new StockAuditPeriod
        {
            Name = name,
            CompanyId = store.CompanyId,
            StoreGroupId = store.StoreGroupId,
            StoreId = store.Id,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Status = "Open",
            Notes = Clean(request.Notes),
            CreatedBy = context.User.Identity?.Name
        };
        db.StockAuditPeriods.Add(period);
        await db.SaveChangesAsync(cancellationToken);

        var rows = await ToRowsAsync([period], db, cancellationToken);
        return Results.Ok(rows[0]);
    }

    private static async Task<IResult> UpdatePeriodAsync(
        Guid id, UpdateStockAuditPeriodRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        var name = Clean(request.Name);
        if (string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest(new { message = "Name is required." });
        }
        if (request.EndDate.Date < request.StartDate.Date)
        {
            return Results.BadRequest(new { message = "End date cannot be before start date." });
        }

        period.Name = name;
        period.StartDate = request.StartDate.Date;
        period.EndDate = request.EndDate.Date;
        period.Notes = Clean(request.Notes);
        await db.SaveChangesAsync(cancellationToken);

        var rows = await ToRowsAsync([period], db, cancellationToken);
        return Results.Ok(rows[0]);
    }

    private static async Task<IResult> ClosePeriodAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        period.Status = "Closed";
        period.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var rows = await ToRowsAsync([period], db, cancellationToken);
        return Results.Ok(rows[0]);
    }

    private static async Task<IResult> ReopenPeriodAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        period.Status = "Open";
        period.ClosedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        var rows = await ToRowsAsync([period], db, cancellationToken);
        return Results.Ok(rows[0]);
    }

    private static async Task<IResult> DeletePeriodAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        period.Deleted = true;
        var scans = await db.StockAuditScans.Where(s => !s.Deleted && s.AuditPeriodId == id).ToListAsync(cancellationToken);
        foreach (var scan in scans)
        {
            scan.Deleted = true;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<StockAuditPeriodRowDto>> ToRowsAsync(List<StockAuditPeriod> periods, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (periods.Count == 0)
        {
            return Array.Empty<StockAuditPeriodRowDto>();
        }

        var periodIds = periods.Select(p => p.Id).ToArray();
        var storeIds = periods.Select(p => p.StoreId).Distinct().ToArray();
        var storeNames = await db.Stores.AsNoTracking().Where(s => storeIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var scanRows = await db.StockAuditScans.AsNoTracking()
            .Where(s => !s.Deleted && periodIds.Contains(s.AuditPeriodId))
            .Select(s => new { s.AuditPeriodId, s.Barcode, s.Quantity })
            .ToListAsync(cancellationToken);
        var aggByPeriod = scanRows.GroupBy(s => s.AuditPeriodId)
            .ToDictionary(g => g.Key, g => (BarcodeCount: g.Select(s => s.Barcode).Distinct(StringComparer.OrdinalIgnoreCase).Count(), TotalQuantity: g.Sum(s => s.Quantity)));

        return periods.Select(p =>
        {
            aggByPeriod.TryGetValue(p.Id, out var agg);
            return new StockAuditPeriodRowDto(
                p.Id, p.Name, p.StoreId, storeNames.GetValueOrDefault(p.StoreId, "Store"),
                p.StartDate, p.EndDate, p.Status, p.Notes, p.ClosedAt, p.CreatedAt,
                agg.BarcodeCount, agg.TotalQuantity);
        }).ToList();
    }

    // ---------------------------------------------------------------- Scanning

    private static async Task<IResult> RecordScanAsync(
        RecordStockAuditScanRequest request, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);

        var barcode = Clean(request.Barcode);
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return Results.BadRequest(new { message = "Barcode is required." });
        }
        if (request.Quantity <= 0)
        {
            return Results.BadRequest(new { message = "Quantity must be greater than zero." });
        }

        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.Where(p => !p.Deleted), context)
            .FirstOrDefaultAsync(p => p.Id == request.AuditPeriodId, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }
        if (period.Status != "Open")
        {
            return Results.BadRequest(new { message = "This audit period is closed. Reopen it before recording more scans." });
        }

        var scanDate = (request.ScanDate ?? DateTime.Today).Date;
        if (scanDate < period.StartDate.Date || scanDate > period.EndDate.Date)
        {
            return Results.BadRequest(new
            {
                message = $"{scanDate:dd-MM-yyyy} is outside this audit period's window ({period.StartDate:dd-MM-yyyy} to {period.EndDate:dd-MM-yyyy})."
            });
        }

        var stock = await db.Stocks.AsNoTracking()
            .Where(s => !s.Deleted && !s.IsOFB && s.StoreId == period.StoreId && s.Barcode.ToLower() == barcode.ToLower())
            .Include(s => s.Product)!.ThenInclude(p => p!.ProductCategory)
            .Include(s => s.Product)!.ThenInclude(p => p!.ProductSubCategory)
            .FirstOrDefaultAsync(cancellationToken);
        if (stock is null)
        {
            var storeName = await db.Stores.AsNoTracking().Where(s => s.Id == period.StoreId).Select(s => s.Name).FirstOrDefaultAsync(cancellationToken);
            return Results.NotFound(new { message = $"Barcode '{barcode}' was not found in {storeName ?? "this store"}'s inventory." });
        }

        var detail = await db.ProductDetails.AsNoTracking().Where(d => !d.Deleted && d.ProductId == stock.ProductId).FirstOrDefaultAsync(cancellationToken);
        var size = ProductMasterEndpoints.DetectProductSize(stock.Product?.Name, detail?.StyleCode, stock.Product?.Descriptions);

        var existing = await db.StockAuditScans
            .Where(s => !s.Deleted && s.AuditPeriodId == period.Id && s.StoreId == period.StoreId
                && s.Barcode.ToLower() == barcode.ToLower() && s.ScanDate == scanDate)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            existing = new StockAuditScan
            {
                AuditPeriodId = period.Id,
                CompanyId = period.CompanyId,
                StoreGroupId = period.StoreGroupId,
                StoreId = period.StoreId,
                ProductId = stock.ProductId,
                StockId = stock.Id,
                Barcode = stock.Barcode,
                ScanDate = scanDate,
                Quantity = request.Quantity,
                ScanCount = 1,
                FirstScannedAt = now,
                LastScannedAt = now,
                ProductName = stock.Product?.Name ?? stock.Barcode,
                CategoryName = stock.Product?.ProductCategory?.Name,
                SubCategoryName = stock.Product?.ProductSubCategory?.Name,
                Color = detail?.BaseColor,
                Size = size,
                Unit = stock.Unit.ToString(),
                MRP = stock.MRP,
                CostPrice = stock.CostPrice
            };
            db.StockAuditScans.Add(existing);
        }
        else
        {
            existing.Quantity += request.Quantity;
            existing.ScanCount += 1;
            existing.LastScannedAt = now;
            existing.ProductName = stock.Product?.Name ?? stock.Barcode;
            existing.CategoryName = stock.Product?.ProductCategory?.Name;
            existing.SubCategoryName = stock.Product?.ProductSubCategory?.Name;
            existing.Color = detail?.BaseColor;
            existing.Size = size;
            existing.Unit = stock.Unit.ToString();
            existing.MRP = stock.MRP;
            existing.CostPrice = stock.CostPrice;
        }

        await db.SaveChangesAsync(cancellationToken);

        var periodTotal = await db.StockAuditScans.AsNoTracking()
            .Where(s => !s.Deleted && s.AuditPeriodId == period.Id && s.Barcode.ToLower() == barcode.ToLower())
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0m;

        return Results.Ok(new RecordStockAuditScanResponse(ToScanRow(existing), existing.Quantity, periodTotal));
    }

    private static async Task<IResult> DeleteScanAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var scan = await WorkspaceScope.ApplyTo(db.StockAuditScans.Where(s => !s.Deleted), context).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (scan is null)
        {
            return Results.NotFound(new { message = "Scan entry was not found." });
        }

        scan.Deleted = true;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static StockAuditScanRowDto ToScanRow(StockAuditScan s) => new(
        s.Id, s.AuditPeriodId, s.Barcode, s.ProductName, s.CategoryName, s.SubCategoryName, s.Color, s.Size, s.Unit,
        s.ScanDate, s.Quantity, s.ScanCount, s.FirstScannedAt, s.LastScannedAt, s.MRP, s.CostPrice);

    // ---------------------------------------------------------------- Scan log / scan summary report

    private static async Task<IResult> ScanLogAsync(
        Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, DateTime? date, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.AsNoTracking().Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 10, 500);

        var query = db.StockAuditScans.AsNoTracking().Where(s => !s.Deleted && s.AuditPeriodId == id);
        if (date.HasValue)
        {
            var day = date.Value.Date;
            query = query.Where(s => s.ScanDate == day);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(s => s.ScanDate).ThenBy(s => s.ProductName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new StockAuditScanLogResponseDto(total, page, pageSize, rows.Select(ToScanRow).ToList()));
    }

    private static async Task<IResult> ScanSummaryAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var rows = await BuildScanSummaryAsync(id, context, db, loggerFactory, cancellationToken);
        return rows is null ? Results.NotFound(new { message = "Audit period was not found." }) : Results.Ok(rows);
    }

    private static async Task<IResult> ScanSummaryCsvAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var rows = await BuildScanSummaryAsync(id, context, db, loggerFactory, cancellationToken);
        if (rows is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        var header = new[] { "Barcode", "Product", "Category", "Sub Category", "Color", "Size", "Unit", "Total Qty", "Date-wise Entries" };
        var lines = new List<string[]> { header };
        lines.AddRange(rows.Select(r => new[]
        {
            r.Barcode, r.ProductName, r.CategoryName ?? string.Empty, r.SubCategoryName ?? string.Empty, r.Color ?? string.Empty, r.Size ?? string.Empty, r.Unit ?? string.Empty,
            r.TotalQuantity.ToString("0.###", CultureInfo.InvariantCulture),
            string.Join(" | ", r.DateEntries.Select(e => $"{e.ScanDate:dd-MM-yyyy}: {e.Quantity.ToString("0.###", CultureInfo.InvariantCulture)}"))
        }));

        return Results.File(CsvBytes(lines), "text/csv", $"Garmetix-Stock-Audit-Scan-Log-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private static async Task<IReadOnlyList<StockAuditScanSummaryRowDto>?> BuildScanSummaryAsync(
        Guid periodId, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.AsNoTracking().Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var scans = await db.StockAuditScans.AsNoTracking().Where(s => !s.Deleted && s.AuditPeriodId == periodId).Take(MaxRowsPerRequest).ToListAsync(cancellationToken);
        return scans.GroupBy(s => s.Barcode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var latest = g.OrderByDescending(s => s.LastScannedAt).First();
                return new StockAuditScanSummaryRowDto(
                    latest.Barcode, latest.ProductName, latest.CategoryName, latest.SubCategoryName, latest.Color, latest.Size, latest.Unit,
                    latest.MRP, latest.CostPrice, g.Sum(s => s.Quantity),
                    g.OrderBy(s => s.ScanDate).Select(s => new StockAuditScanDateEntryDto(s.ScanDate, s.Quantity, s.ScanCount)).ToList());
            })
            .OrderBy(r => r.CategoryName)
            .ThenBy(r => r.ProductName)
            .ToList();
    }

    // ---------------------------------------------------------------- Discrepancy report (audited vs current)

    private static async Task<IResult> DiscrepancyAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var result = await BuildDiscrepancyAsync(id, context, db, loggerFactory, cancellationToken);
        return result is null ? Results.NotFound(new { message = "Audit period was not found." }) : Results.Ok(result);
    }

    private static async Task<IResult> DiscrepancyCsvAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var result = await BuildDiscrepancyAsync(id, context, db, loggerFactory, cancellationToken);
        if (result is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        var header = new[] { "Barcode", "Product", "Category", "Sub Category", "Color", "Size", "Unit", "Audited Qty", "Current Qty", "Variance", "MRP", "Cost Price", "Audited MRP Value", "Current MRP Value", "Audited Cost Value", "Current Cost Value", "Status" };
        var lines = new List<string[]> { header };
        lines.AddRange(result.Rows.Select(DiscrepancyCsvRow));

        return Results.File(CsvBytes(lines), "text/csv", $"Garmetix-Stock-Audit-Discrepancy-{SanitizeFileSegment(result.AuditPeriodName)}.csv");
    }

    private static async Task<IResult> DiscrepancyExcelAsync(Guid id, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var result = await BuildDiscrepancyAsync(id, context, db, loggerFactory, cancellationToken);
        if (result is null)
        {
            return Results.NotFound(new { message = "Audit period was not found." });
        }

        var sheets = new List<XlsxSheet>
        {
            new("Summary", ["Field", "Value"], new[]
            {
                new[] { "Report", "Stock Audit Discrepancy" },
                new[] { "Audit Period", result.AuditPeriodName },
                new[] { "Generated At UTC", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture) },
                new[] { "Total Items", result.Summary.TotalItems.ToString(CultureInfo.InvariantCulture) },
                new[] { "Matched", result.Summary.MatchedCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Mismatch", result.Summary.MismatchCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Not Counted", result.Summary.NotCountedCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Missing In System", result.Summary.MissingInSystemCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Total Audited MRP Value", result.Summary.TotalAuditedMrpValue.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Total Current MRP Value", result.Summary.TotalCurrentMrpValue.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Note", "Audited quantity is the sum of every scan across the audit period's dates for that barcode. Current quantity is live system stock at report time." }
            }),
            new("Discrepancy", ["Barcode", "Product", "Category", "Sub Category", "Color", "Size", "Unit", "Audited Qty", "Current Qty", "Variance", "MRP", "Cost Price", "Audited MRP Value", "Current MRP Value", "Audited Cost Value", "Current Cost Value", "Status"],
                result.Rows.Select(DiscrepancyCsvRow))
        };

        var bytes = SimpleXlsxBuilder.Build(sheets);
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Garmetix-Stock-Audit-Discrepancy-{SanitizeFileSegment(result.AuditPeriodName)}.xlsx");
    }

    private static string[] DiscrepancyCsvRow(StockAuditDiscrepancyRowDto r) =>
    [
        r.Barcode, r.ProductName, r.CategoryName ?? string.Empty, r.SubCategoryName ?? string.Empty, r.Color ?? string.Empty, r.Size ?? string.Empty, r.Unit ?? string.Empty,
        r.AuditedQuantity.ToString("0.###", CultureInfo.InvariantCulture), r.CurrentQuantity.ToString("0.###", CultureInfo.InvariantCulture), r.Variance.ToString("0.###", CultureInfo.InvariantCulture),
        r.MRP.ToString("0.00", CultureInfo.InvariantCulture), r.CostPrice.ToString("0.00", CultureInfo.InvariantCulture),
        r.AuditedMrpValue.ToString("0.00", CultureInfo.InvariantCulture), r.CurrentMrpValue.ToString("0.00", CultureInfo.InvariantCulture),
        r.AuditedCostValue.ToString("0.00", CultureInfo.InvariantCulture), r.CurrentCostValue.ToString("0.00", CultureInfo.InvariantCulture),
        r.Status
    ];

    private static async Task<StockAuditDiscrepancyResponseDto?> BuildDiscrepancyAsync(
        Guid periodId, HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.AsNoTracking().Where(p => !p.Deleted), context).FirstOrDefaultAsync(p => p.Id == periodId, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var scans = await db.StockAuditScans.AsNoTracking().Where(s => !s.Deleted && s.AuditPeriodId == period.Id).Take(MaxRowsPerRequest).ToListAsync(cancellationToken);
        var auditedByBarcode = scans.GroupBy(s => s.Barcode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (Quantity: g.Sum(s => s.Quantity), Latest: g.OrderByDescending(s => s.LastScannedAt).First()),
                StringComparer.OrdinalIgnoreCase);

        var liveStocks = await db.Stocks.AsNoTracking()
            .Where(s => !s.Deleted && !s.IsOFB && s.StoreId == period.StoreId)
            .Include(s => s.Product)!.ThenInclude(p => p!.ProductCategory)
            .Include(s => s.Product)!.ThenInclude(p => p!.ProductSubCategory)
            .Take(MaxRowsPerRequest)
            .ToListAsync(cancellationToken);

        var productIds = liveStocks.Select(s => s.ProductId).Distinct().ToArray();
        var details = await db.ProductDetails.AsNoTracking().Where(d => !d.Deleted && productIds.Contains(d.ProductId)).ToListAsync(cancellationToken);
        var detailByProduct = details.GroupBy(d => d.ProductId).ToDictionary(g => g.Key, g => g.First());

        var rows = new List<StockAuditDiscrepancyRowDto>();
        var seenBarcodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stock in liveStocks)
        {
            seenBarcodes.Add(stock.Barcode);
            var detail = detailByProduct.GetValueOrDefault(stock.ProductId);
            var size = ProductMasterEndpoints.DetectProductSize(stock.Product?.Name, detail?.StyleCode, stock.Product?.Descriptions);
            var hasAudit = auditedByBarcode.TryGetValue(stock.Barcode, out var audit);
            var auditedQty = hasAudit ? audit.Quantity : 0m;
            var currentQty = stock.CurrentStock;
            var variance = auditedQty - currentQty;
            var status = !hasAudit ? "Not Counted" : Math.Abs(variance) <= 0.01m ? "Matched" : "Mismatch";

            rows.Add(new StockAuditDiscrepancyRowDto(
                stock.Barcode,
                stock.Product?.Name ?? stock.Barcode,
                stock.Product?.ProductCategory?.Name,
                stock.Product?.ProductSubCategory?.Name,
                detail?.BaseColor,
                size,
                stock.Unit.ToString(),
                auditedQty,
                currentQty,
                variance,
                stock.MRP,
                stock.CostPrice,
                Math.Round(auditedQty * stock.MRP, 2),
                Math.Round(currentQty * stock.MRP, 2),
                Math.Round(auditedQty * stock.CostPrice, 2),
                Math.Round(currentQty * stock.CostPrice, 2),
                status));
        }

        foreach (var kvp in auditedByBarcode)
        {
            if (seenBarcodes.Contains(kvp.Key))
            {
                continue;
            }

            var latest = kvp.Value.Latest;
            rows.Add(new StockAuditDiscrepancyRowDto(
                latest.Barcode, latest.ProductName, latest.CategoryName, latest.SubCategoryName, latest.Color, latest.Size, latest.Unit,
                kvp.Value.Quantity, 0m, kvp.Value.Quantity, latest.MRP, latest.CostPrice,
                Math.Round(kvp.Value.Quantity * latest.MRP, 2), 0m,
                Math.Round(kvp.Value.Quantity * latest.CostPrice, 2), 0m,
                "Missing In System"));
        }

        var ordered = rows.OrderBy(r => r.CategoryName).ThenBy(r => r.ProductName).ToList();
        var summary = new StockAuditDiscrepancySummaryDto(
            ordered.Count,
            ordered.Count(r => r.Status == "Matched"),
            ordered.Count(r => r.Status == "Mismatch"),
            ordered.Count(r => r.Status == "Not Counted"),
            ordered.Count(r => r.Status == "Missing In System"),
            ordered.Sum(r => r.Variance),
            ordered.Sum(r => r.AuditedMrpValue),
            ordered.Sum(r => r.CurrentMrpValue),
            ordered.Sum(r => r.AuditedCostValue),
            ordered.Sum(r => r.CurrentCostValue));

        return new StockAuditDiscrepancyResponseDto(period.Id, period.Name, summary, ordered);
    }

    // ---------------------------------------------------------------- Stock Snapshot (Category / Size / Color)
    // Rows always carry all three dimensions (Category, Color, Size, Qty, MRP Value, Cost Value) - "Group By"
    // just picks which dimension "Group Data" filters on (e.g. Group By=Color, Group Data=Blue shows only Blue
    // rows); it never collapses the table down to a single dimension column. AvailableGroupValues is the
    // distinct value list for the selected dimension, computed before the groupValue filter is applied, so the
    // frontend's "Group Data" dropdown can offer "All" plus every value actually present in stock.

    private static async Task<IResult> StockSummaryAsync(
        HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, string? groupBy, string? groupValue, string? source, Guid? auditPeriodId, Guid? storeId, CancellationToken cancellationToken)
    {
        var result = await BuildStockSummaryAsync(context, db, loggerFactory, groupBy, groupValue, source, auditPeriodId, storeId, cancellationToken);
        return result is null
            ? Results.BadRequest(new { message = "source=audited requires a valid auditPeriodId for an audit period you can access." })
            : Results.Ok(result);
    }

    private static async Task<IResult> StockSummaryCsvAsync(
        HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, string? groupBy, string? groupValue, string? source, Guid? auditPeriodId, Guid? storeId, CancellationToken cancellationToken)
    {
        var result = await BuildStockSummaryAsync(context, db, loggerFactory, groupBy, groupValue, source, auditPeriodId, storeId, cancellationToken);
        if (result is null)
        {
            return Results.BadRequest(new { message = "source=audited requires a valid auditPeriodId for an audit period you can access." });
        }

        var header = new[] { "Product Category", "Color", "Size", "Qty", "MRP Value", "Cost Value" };
        var lines = new List<string[]> { header };
        lines.AddRange(result.Rows.Select(StockSummaryCsvRow));

        return Results.File(CsvBytes(lines), "text/csv", $"Garmetix-Stock-Snapshot-{result.GroupBy}-{result.Source}-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    private static async Task<IResult> StockSummaryExcelAsync(
        HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, string? groupBy, string? groupValue, string? source, Guid? auditPeriodId, Guid? storeId, CancellationToken cancellationToken)
    {
        var result = await BuildStockSummaryAsync(context, db, loggerFactory, groupBy, groupValue, source, auditPeriodId, storeId, cancellationToken);
        if (result is null)
        {
            return Results.BadRequest(new { message = "source=audited requires a valid auditPeriodId for an audit period you can access." });
        }

        var sheets = new List<XlsxSheet>
        {
            new("Summary", ["Field", "Value"], new[]
            {
                new[] { "Report", "Stock Snapshot" },
                new[] { "Grouped By", result.GroupBy },
                new[] { "Group Filter", string.IsNullOrWhiteSpace(result.GroupValue) ? "All" : result.GroupValue },
                new[] { "Source", result.Source == "audited" ? $"Audited ({result.AuditPeriodName})" : "Current Live Stock" },
                new[] { "Generated At UTC", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture) },
                new[] { "Total Products", result.TotalProductCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Total Quantity", result.TotalQuantity.ToString("0.###", CultureInfo.InvariantCulture) },
                new[] { "Total MRP Value", result.TotalMrpValue.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Total Cost Value", result.TotalCostValue.ToString("0.00", CultureInfo.InvariantCulture) }
            }),
            new("Stock Snapshot", ["Product Category", "Color", "Size", "Qty", "MRP Value", "Cost Value"], result.Rows.Select(StockSummaryCsvRow))
        };

        var bytes = SimpleXlsxBuilder.Build(sheets);
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Garmetix-Stock-Snapshot-{result.GroupBy}-{result.Source}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }

    private static string[] StockSummaryCsvRow(StockSummaryRowDto r) =>
    [
        r.CategoryName, r.ColorName, r.SizeName, r.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
        r.MrpValue.ToString("0.00", CultureInfo.InvariantCulture), r.CostValue.ToString("0.00", CultureInfo.InvariantCulture)
    ];

    private static async Task<StockSummaryResponseDto?> BuildStockSummaryAsync(
        HttpContext context, GarmetixDbContext db, ILoggerFactory loggerFactory, string? groupByRaw, string? groupValueRaw, string? sourceRaw, Guid? auditPeriodId, Guid? storeId, CancellationToken cancellationToken)
    {
        await RepairAsync(db, loggerFactory, cancellationToken);
        var groupBy = (groupByRaw ?? "category").Trim().ToLowerInvariant();
        var source = (sourceRaw ?? "current").Trim().ToLowerInvariant();
        var groupValue = Clean(groupValueRaw);
        if (groupValue is not null && string.Equals(groupValue, "all", StringComparison.OrdinalIgnoreCase))
        {
            groupValue = null;
        }

        List<StockSnapshotItem> items;
        Guid? resolvedAuditPeriodId = null;
        string? resolvedAuditPeriodName = null;
        Guid? resolvedStoreId = storeId;

        if (source == "audited")
        {
            if (auditPeriodId is null || auditPeriodId.Value == Guid.Empty)
            {
                return null;
            }

            var period = await WorkspaceScope.ApplyTo(db.StockAuditPeriods.AsNoTracking().Where(p => !p.Deleted), context)
                .FirstOrDefaultAsync(p => p.Id == auditPeriodId.Value, cancellationToken);
            if (period is null)
            {
                return null;
            }

            resolvedAuditPeriodId = period.Id;
            resolvedAuditPeriodName = period.Name;
            resolvedStoreId = period.StoreId;

            var scans = await db.StockAuditScans.AsNoTracking().Where(s => !s.Deleted && s.AuditPeriodId == period.Id).Take(MaxRowsPerRequest).ToListAsync(cancellationToken);
            items = scans.GroupBy(s => s.Barcode, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(s => s.LastScannedAt).First();
                    return new StockSnapshotItem(latest.CategoryName, latest.Color, latest.Size, latest.MRP, latest.CostPrice, g.Sum(s => s.Quantity));
                })
                .ToList();
        }
        else
        {
            var stocksQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking().Where(s => !s.Deleted && !s.IsOFB), context)
                .Include(s => s.Product)!.ThenInclude(p => p!.ProductCategory)
                .AsQueryable();
            if (storeId.HasValue && storeId.Value != Guid.Empty)
            {
                stocksQuery = stocksQuery.Where(s => s.StoreId == storeId.Value);
            }

            var stocks = await stocksQuery.Take(MaxRowsPerRequest).ToListAsync(cancellationToken);
            var productIds = stocks.Select(s => s.ProductId).Distinct().ToArray();
            var details = await db.ProductDetails.AsNoTracking().Where(d => !d.Deleted && productIds.Contains(d.ProductId)).ToListAsync(cancellationToken);
            var detailByProduct = details.GroupBy(d => d.ProductId).ToDictionary(g => g.Key, g => g.First());

            items = stocks.Select(s =>
            {
                var detail = detailByProduct.GetValueOrDefault(s.ProductId);
                var size = ProductMasterEndpoints.DetectProductSize(s.Product?.Name, detail?.StyleCode, s.Product?.Descriptions);
                return new StockSnapshotItem(s.Product?.ProductCategory?.Name, detail?.BaseColor, size, s.MRP, s.CostPrice, s.CurrentStock);
            }).ToList();
        }

        var availableGroupValues = items
            .Select(item => GroupDimensionValue(groupBy, item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filtered = groupValue is null
            ? items
            : items.Where(item => string.Equals(GroupDimensionValue(groupBy, item), groupValue, StringComparison.OrdinalIgnoreCase)).ToList();

        var rows = filtered
            .GroupBy(item => (
                Category: string.IsNullOrWhiteSpace(item.CategoryName) ? "Uncategorized" : item.CategoryName!,
                Color: string.IsNullOrWhiteSpace(item.Color) ? "Unspecified" : item.Color!,
                Size: string.IsNullOrWhiteSpace(item.Size) ? "Unspecified" : item.Size!))
            .Select(g => new StockSummaryRowDto(
                g.Key.Category, g.Key.Color, g.Key.Size, g.Count(),
                g.Sum(x => x.Quantity), Math.Round(g.Sum(x => x.Quantity * x.MRP), 2), Math.Round(g.Sum(x => x.Quantity * x.CostPrice), 2)))
            .OrderByDescending(r => r.MrpValue)
            .ToList();

        return new StockSummaryResponseDto(
            source, groupBy, groupValue, resolvedAuditPeriodId, resolvedAuditPeriodName, resolvedStoreId,
            filtered.Count, rows.Sum(r => r.Quantity), rows.Sum(r => r.MrpValue), rows.Sum(r => r.CostValue),
            availableGroupValues, rows);
    }

    private sealed record StockSnapshotItem(string? CategoryName, string? Color, string? Size, decimal MRP, decimal CostPrice, decimal Quantity);

    private static string GroupDimensionValue(string groupBy, StockSnapshotItem item) => groupBy switch
    {
        "size" => string.IsNullOrWhiteSpace(item.Size) ? "Unspecified" : item.Size!,
        "color" => string.IsNullOrWhiteSpace(item.Color) ? "Unspecified" : item.Color!,
        _ => string.IsNullOrWhiteSpace(item.CategoryName) ? "Uncategorized" : item.CategoryName!
    };

    // ---------------------------------------------------------------- Shared helpers

    private static Task RepairAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        => DatabaseSchemaRepairService.RepairStockAuditStorageAsync(db, loggerFactory.CreateLogger("StockAuditStorageRepair"), cancellationToken);

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string SanitizeFileSegment(string value)
    {
        var sanitized = Regex.Replace(value, "[^A-Za-z0-9-]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "Period" : sanitized;
    }

    private static byte[] CsvBytes(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(CsvCell)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string CsvCell(string? value)
    {
        value ??= string.Empty;
        var escaped = value.Replace("\"", "\"\"");
        return escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r') ? $"\"{escaped}\"" : escaped;
    }
}
