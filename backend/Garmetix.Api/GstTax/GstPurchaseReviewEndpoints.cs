using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// Purchase GST Review (spec Stage GST-8): line-level GST correctness for Purchase invoices, using the
/// same configurable Rate Master resolver (Stage GST-5) as Sale GST Review (Stage GST-7), cross-linked to
/// open GST Audit Engine findings (Stage GST-6) raised for the same invoices, plus ITC (Input Tax Credit)
/// eligibility warnings per invoice. Read-only - no accounting mutation happens here. There is no existing
/// hardcoded-threshold "Purchase Review" tool to merge with (unlike Sale Review) - this is the first Purchase
/// GST review surface in the codebase. Purchase-import (OCR) batches are covered automatically once posted,
/// since posting writes into the same PurchaseInvoices/PurchaseInvoiceItems tables this reads from; unposted
/// import drafts are not real financial invoices yet and are intentionally out of scope here.
/// Capped at 2000 lines per request (mirrors the Stage GST-6/7 per-run cap) since each line resolves live
/// against the Rate Master - narrow the date range for a full historical sweep.
/// </summary>
public static class GstPurchaseReviewEndpoints
{
    private const int MaxLinesPerRequest = 2000;

    public static RouteGroupBuilder MapGstPurchaseReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst/purchase-review")
            .WithTags("GST & Taxes - Purchase Review")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("", GetAsync);
        return group;
    }

    private static async Task<GstPurchaseReviewResponseDto> GetAsync(
        HttpContext context,
        GarmetixDbContext db,
        GstRateResolutionService rateResolver,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
        Guid? vendorId = null,
        string? search = null,
        bool onlyIssues = false,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(db, loggerFactory.CreateLogger("GstTaxStorageRepair"), cancellationToken);

        var today = DateTime.Today;
        var from = (fromDate ?? today.AddDays(-30)).Date;
        var to = (toDate ?? today).Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);
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
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InwardNumber,
                i.OnDate,
                i.InterState,
                i.StoreId,
                i.VendorId,
                VendorName = i.VendorName ?? (i.Vendor != null ? i.Vendor.Name : "Vendor"),
                i.VendorGSTIN
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return new GstPurchaseReviewResponseDto(from, to, storeId, vendorId, term, page, pageSize, 0, EmptySummary(), Array.Empty<GstPurchaseReviewInvoiceDto>(), Array.Empty<GstPurchaseReviewLineDto>());
        }

        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        var invoiceMap = invoices.ToDictionary(i => i.Id);

        var storeIds = invoices.Where(i => i.StoreId.HasValue).Select(i => i.StoreId!.Value).Distinct().ToArray();
        var storeNames = await db.Stores.AsNoTracking()
            .Where(s => storeIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

        var vendorIds = invoices.Select(i => i.VendorId).Distinct().ToArray();
        var vendors = await db.Vendors.AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id))
            .Select(v => new GstItcEligibilityService.VendorGstSnapshot(v.Id, v.GSTVerified, v.GSTRegistrationStatus))
            .ToDictionaryAsync(v => v.VendorId, cancellationToken);

        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(l => invoiceIds.Contains(l.InvoiceId))
            .Take(MaxLinesPerRequest)
            .ToListAsync(cancellationToken);

        var categoryIds = items.Where(l => l.ProductCategoryId.HasValue).Select(l => l.ProductCategoryId!.Value).Distinct().ToArray();
        var categoryNames = await db.ProductCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var findingCounts = await db.GstAuditFindings.AsNoTracking()
            .Where(f => !f.Deleted && f.Status == "Open" && f.ModuleArea == "Purchase" && f.PurchaseInvoiceId.HasValue && invoiceIds.Contains(f.PurchaseInvoiceId.Value))
            .GroupBy(f => f.PurchaseInvoiceId!.Value)
            .Select(g => new { PurchaseInvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PurchaseInvoiceId, g => g.Count, cancellationToken);

        var lines = new List<GstPurchaseReviewLineDto>(items.Count);
        foreach (var item in items)
        {
            if (!invoiceMap.TryGetValue(item.InvoiceId, out var invoice))
            {
                continue;
            }

            var storeName = invoice.StoreId.HasValue ? storeNames.GetValueOrDefault(invoice.StoreId.Value) : null;
            var categoryName = item.ProductCategoryId.HasValue ? categoryNames.GetValueOrDefault(item.ProductCategoryId.Value) : null;
            var basicAfterDiscount = item.MRP - item.DiscountAmount;
            var resolved = await rateResolver.ResolveAsync(
                new GstRateResolveRequest(item.HSNCode, categoryName, basicAfterDiscount, invoice.OnDate, !invoice.InterState),
                cancellationToken);

            string status;
            if (!resolved.Success)
            {
                status = "Not Configured";
            }
            else if (Math.Abs(resolved.TaxRate - item.TaxPercentage) <= 0.01m)
            {
                status = "OK";
            }
            else
            {
                status = "Rate Mismatch";
            }

            var resolvedTaxAmount = resolved.Success ? Math.Round(item.TaxableAmount * (resolved.TaxRate / 100m), 2) : item.TaxAmount;
            var isGarmentThreshold = resolved.Success
                && string.Equals(categoryName, "Garments", StringComparison.OrdinalIgnoreCase)
                && resolved.Source == "Rate Master";

            lines.Add(new GstPurchaseReviewLineDto(
                invoice.Id,
                item.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                string.IsNullOrWhiteSpace(storeName) ? "Store" : storeName,
                invoice.VendorName,
                item.ProductId,
                item.ProductName ?? "Item",
                item.Barcode,
                item.HSNCode,
                categoryName,
                item.BilledQuantity,
                item.MRP,
                item.DiscountAmount,
                item.TaxableAmount,
                item.TaxPercentage,
                resolved.Success ? resolved.TaxRate : item.TaxPercentage,
                item.TaxAmount,
                resolvedTaxAmount,
                resolvedTaxAmount - item.TaxAmount,
                resolved.Source,
                resolved.RuleName,
                isGarmentThreshold,
                status));
        }

        if (onlyIssues)
        {
            lines = lines.Where(l => l.Status != "OK").ToList();
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            lines = lines.Where(l =>
                l.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.VendorName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.Barcode.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var invoiceSummaries = lines
            .GroupBy(l => l.PurchaseInvoiceId)
            .Select(g =>
            {
                var first = g.First();
                var issueCount = g.Count(l => l.Status != "OK");
                var sourceInvoice = invoiceMap[first.PurchaseInvoiceId];
                var (itcStatus, itcNote) = GstItcEligibilityService.Resolve(sourceInvoice.VendorGSTIN, sourceInvoice.VendorId, vendors);
                return new GstPurchaseReviewInvoiceDto(
                    first.PurchaseInvoiceId,
                    first.InvoiceNumber,
                    sourceInvoice.InwardNumber,
                    first.OnDate,
                    first.StoreName,
                    sourceInvoice.VendorId,
                    first.VendorName,
                    sourceInvoice.VendorGSTIN,
                    sourceInvoice.InterState,
                    g.Count(),
                    issueCount,
                    findingCounts.GetValueOrDefault(first.PurchaseInvoiceId),
                    g.Sum(l => l.StoredTaxAmount),
                    g.Sum(l => l.ResolvedTaxAmount),
                    g.Sum(l => l.TaxDifferenceAmount),
                    issueCount == 0 ? "OK" : "Needs Review",
                    itcStatus,
                    itcNote);
            })
            .OrderByDescending(i => i.OnDate)
            .ThenBy(i => i.InvoiceNumber)
            .ToList();

        var totalLines = lines.Count;
        var pagedLines = lines
            .OrderByDescending(l => l.OnDate)
            .ThenBy(l => l.InvoiceNumber)
            .ThenBy(l => l.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var summary = new GstPurchaseReviewSummaryDto(
            invoiceSummaries.Count,
            lines.Count,
            lines.Count(l => l.Status == "Rate Mismatch"),
            lines.Count(l => l.Status == "Not Configured"),
            invoiceSummaries.Sum(i => i.OpenAuditFindingCount),
            invoiceSummaries.Count(i => i.ItcStatus == "Not Eligible"),
            invoiceSummaries.Count(i => i.ItcStatus == "At Risk"),
            invoiceSummaries.Count(i => i.ItcStatus == "Unverified"),
            lines.Sum(l => l.TaxableValue),
            lines.Sum(l => l.StoredTaxAmount),
            lines.Sum(l => l.ResolvedTaxAmount),
            lines.Sum(l => l.TaxDifferenceAmount));

        return new GstPurchaseReviewResponseDto(from, to, storeId, vendorId, term, page, pageSize, totalLines, summary, invoiceSummaries, pagedLines);
    }

    private static GstPurchaseReviewSummaryDto EmptySummary() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
