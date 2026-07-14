using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.GstReturns;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// ITC Register (spec Stage GST-9): an auto-computed, invoice-level Input Tax Credit register built from real
/// Purchase invoice data, reusing the exact ITC eligibility heuristic Stage GST-8 (Purchase GST Review)
/// introduced (<see cref="GstItcEligibilityService"/>) so the two features never disagree. This closes a real
/// gap in the pre-existing GSTR-3B builder (/api/gst-returns): that form's "4 ITC" section is entirely manual
/// entry (its own export explicitly notes "Manual/separate GST module export. Not linked to Billing/Purchase
/// yet.") - this register is the first ITC figure in the codebase actually computed from posted Purchase
/// invoices. Read-only - no accounting mutation happens here, and this does not replace or alter the manual
/// GSTR-3B ITC block, which is left completely untouched; the register is a source an accountant can cross-check
/// against before typing GSTR-3B's ITC numbers in by hand.
/// Reuses the existing <see cref="SimpleXlsxBuilder"/>/<see cref="XlsxSheet"/> Excel-writer from the GST Returns
/// module for its CA-ready Excel export, rather than duplicating an OOXML writer.
/// Capped at 2000 rows per request (mirrors every other GST & Taxes review/audit endpoint's cap).
/// </summary>
public static class GstItcRegisterEndpoints
{
    private const int MaxRowsPerRequest = 2000;

    public static RouteGroupBuilder MapGstItcRegisterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst/itc-register")
            .WithTags("GST & Taxes - ITC Register")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("", GetAsync);
        group.MapGet("/csv", DownloadCsvAsync);
        group.MapGet("/excel", DownloadExcelAsync);
        return group;
    }

    private static async Task<GstItcRegisterResponseDto> GetAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        Guid? vendorId = null,
        string? search = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var (from, to, allRows) = await BuildRegisterAsync(context, db, loggerFactory, fromDate, toDate, storeId, vendorId, search, cancellationToken);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);

        var pagedRows = allRows
            .OrderByDescending(r => r.OnDate)
            .ThenBy(r => r.InvoiceNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var summary = BuildSummary(allRows);
        return new GstItcRegisterResponseDto(from, to, storeId, vendorId, search?.Trim(), page, pageSize, allRows.Count, summary, pagedRows);
    }

    private static async Task<IResult> DownloadCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        Guid? vendorId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var (from, to, rows) = await BuildRegisterAsync(context, db, loggerFactory, fromDate, toDate, storeId, vendorId, search, cancellationToken);

        var header = new[] { "Invoice", "Inward No", "Date", "Vendor", "GSTIN", "Inter-State", "Taxable Value", "CGST", "SGST", "IGST", "Tax", "ITC Status", "Eligible ITC", "Note" };
        var lines = new List<string[]> { header };
        lines.AddRange(rows.OrderByDescending(r => r.OnDate).ThenBy(r => r.InvoiceNumber).Select(row => new[]
        {
            row.InvoiceNumber, row.InwardNumber, row.OnDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), row.VendorName,
            row.VendorGSTIN ?? string.Empty, row.InterState ? "Yes" : "No",
            row.TaxableValue.ToString("0.00", CultureInfo.InvariantCulture), row.CgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.SgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
            row.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture), row.ItcStatus,
            row.EligibleItcAmount.ToString("0.00", CultureInfo.InvariantCulture), row.ItcNote
        }));

        return Results.File(CsvBytes(lines), "text/csv", $"Garmetix-ITC-Register-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.csv");
    }

    private static async Task<IResult> DownloadExcelAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        Guid? vendorId = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var (from, to, rows) = await BuildRegisterAsync(context, db, loggerFactory, fromDate, toDate, storeId, vendorId, search, cancellationToken);
        var summary = BuildSummary(rows);
        var ordered = rows.OrderByDescending(r => r.OnDate).ThenBy(r => r.InvoiceNumber).ToList();

        var sheets = new List<XlsxSheet>
        {
            new("Summary", ["Field", "Value"], new[]
            {
                new[] { "Report", "ITC Register" },
                new[] { "From", from.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) },
                new[] { "To", to.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) },
                new[] { "Generated At UTC", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture) },
                new[] { "Invoices", summary.InvoiceCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Eligible", summary.EligibleCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "At Risk", summary.AtRiskCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Unverified", summary.UnverifiedCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Not Eligible", summary.NotEligibleCount.ToString(CultureInfo.InvariantCulture) },
                new[] { "Total Taxable Value", summary.TotalTaxableValue.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Total Tax", summary.TotalTaxAmount.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Total Eligible ITC", summary.TotalEligibleItcAmount.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Total Ineligible ITC", summary.TotalIneligibleItcAmount.ToString("0.00", CultureInfo.InvariantCulture) },
                new[] { "Note", "Auto-computed from Purchase invoices. ITC status is a heuristic - confirm with your accountant before filing GSTR-3B." }
            }),
            new("ITC Register", ["Invoice", "Inward No", "Date", "Vendor", "GSTIN", "Inter-State", "Taxable Value", "CGST", "SGST", "IGST", "Tax", "ITC Status", "Eligible ITC", "Note"],
                ordered.Select(row => new[]
                {
                    row.InvoiceNumber, row.InwardNumber, row.OnDate.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), row.VendorName,
                    row.VendorGSTIN ?? string.Empty, row.InterState ? "Yes" : "No",
                    row.TaxableValue.ToString("0.00", CultureInfo.InvariantCulture), row.CgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    row.SgstAmount.ToString("0.00", CultureInfo.InvariantCulture), row.IgstAmount.ToString("0.00", CultureInfo.InvariantCulture),
                    row.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture), row.ItcStatus,
                    row.EligibleItcAmount.ToString("0.00", CultureInfo.InvariantCulture), row.ItcNote
                }))
        };

        var bytes = SimpleXlsxBuilder.Build(sheets);
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Garmetix-ITC-Register-{from:yyyy-MM-dd}-to-{to:yyyy-MM-dd}.xlsx");
    }

    private static async Task<(DateTime From, DateTime To, List<GstItcRegisterRowDto> Rows)> BuildRegisterAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? storeId,
        Guid? vendorId,
        string? search,
        CancellationToken cancellationToken)
    {
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(db, loggerFactory.CreateLogger("GstTaxStorageRepair"), cancellationToken);

        var today = DateTime.Today;
        var from = (fromDate ?? today.AddDays(-30)).Date;
        var to = (toDate ?? today).Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var inclusiveTo = to.AddDays(1);
        var term = search?.Trim();

        var invoiceQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking().Where(i => !i.Deleted), context)
            .Where(i => i.OnDate >= from && i.OnDate < inclusiveTo && i.InvoiceStatus != InvoiceStatus.Cancelled);

        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            invoiceQuery = invoiceQuery.Where(i => i.StoreId == storeId.Value);
        }

        if (vendorId.HasValue && vendorId.Value != Guid.Empty)
        {
            invoiceQuery = invoiceQuery.Where(i => i.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            invoiceQuery = invoiceQuery.Where(i =>
                i.InvoiceNumber.Contains(term) ||
                i.InwardNumber.Contains(term) ||
                (i.VendorName != null && i.VendorName.Contains(term)));
        }

        var invoices = await invoiceQuery
            .OrderByDescending(i => i.OnDate)
            .Take(MaxRowsPerRequest)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InwardNumber,
                i.OnDate,
                i.InterState,
                i.VendorId,
                VendorName = i.VendorName ?? (i.Vendor != null ? i.Vendor.Name : "Vendor"),
                i.VendorGSTIN,
                i.NetAmount,
                i.CGSTAmount,
                i.SGSTAmount,
                i.IGSTAmount,
                i.TaxAmount
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return (from, to, new List<GstItcRegisterRowDto>());
        }

        var vendorIds = invoices.Select(i => i.VendorId).Distinct().ToArray();
        var vendors = await db.Vendors.AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id))
            .Select(v => new GstItcEligibilityService.VendorGstSnapshot(v.Id, v.GSTVerified, v.GSTRegistrationStatus))
            .ToDictionaryAsync(v => v.VendorId, cancellationToken);

        var rows = invoices.Select(invoice =>
        {
            var (status, note) = GstItcEligibilityService.Resolve(invoice.VendorGSTIN, invoice.VendorId, vendors);
            var cgst = invoice.CGSTAmount ?? invoice.TaxAmount / 2;
            var sgst = invoice.SGSTAmount ?? invoice.TaxAmount / 2;
            var igst = invoice.IGSTAmount ?? 0;
            var eligibleAmount = GstItcEligibilityService.IsClaimable(status) ? invoice.TaxAmount : 0m;

            return new GstItcRegisterRowDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InwardNumber,
                invoice.OnDate,
                invoice.VendorName,
                invoice.VendorGSTIN,
                invoice.InterState,
                Math.Round(invoice.NetAmount, 2),
                Math.Round(cgst, 2),
                Math.Round(sgst, 2),
                Math.Round(igst, 2),
                Math.Round(invoice.TaxAmount, 2),
                status,
                note,
                Math.Round(eligibleAmount, 2));
        }).ToList();

        return (from, to, rows);
    }

    private static GstItcRegisterSummaryDto BuildSummary(IReadOnlyList<GstItcRegisterRowDto> rows) => new(
        rows.Count,
        rows.Count(r => r.ItcStatus == GstItcEligibilityService.Eligible),
        rows.Count(r => r.ItcStatus == GstItcEligibilityService.AtRisk),
        rows.Count(r => r.ItcStatus == GstItcEligibilityService.Unverified),
        rows.Count(r => r.ItcStatus == GstItcEligibilityService.NotEligible),
        rows.Sum(r => r.TaxableValue),
        rows.Sum(r => r.TaxAmount),
        rows.Sum(r => r.EligibleItcAmount),
        rows.Sum(r => r.TaxAmount - r.EligibleItcAmount));

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
