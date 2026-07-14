using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// Sale GST Review (spec Stage GST-7): line-level GST correctness for Sale invoices, using the
/// configurable Rate Master resolver (Stage GST-5) rather than a hardcoded apparel threshold, cross-linked
/// to open GST Audit Engine findings (Stage GST-6) raised for the same invoices. Read-only - no accounting
/// mutation happens here. The existing legacy-parity apparel-threshold "Sale Review" (/api/sale-review,
/// hardcoded 5% up to Rs.2,499 / 18% above, with its own Option-B correction+posting workflow) is left
/// completely untouched; this is an additive, configurable-rate-driven view that sits alongside it on the
/// same GST & Taxes "Sale GST Review" page rather than replacing or duplicating its posting logic.
/// Capped at 2000 lines per request (mirrors the Stage GST-6 audit engine's per-run cap) since each line
/// resolves live against the Rate Master - narrow the date range for a full historical sweep.
/// </summary>
public static class GstSaleReviewEndpoints
{
    private const int MaxLinesPerRequest = 2000;

    public static RouteGroupBuilder MapGstSaleReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gst/sale-review")
            .WithTags("GST & Taxes - Sale Review")
            .RequireAuthorization(GarmetixPolicies.Gst);

        group.MapGet("", GetAsync);
        return group;
    }

    private static async Task<GstSaleReviewResponseDto> GetAsync(
        HttpContext context,
        GarmetixDbContext db,
        GstRateResolutionService rateResolver,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Guid? storeId = null,
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

        var invoiceQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking().Where(i => !i.Deleted), context)
            .Where(i => i.OnDate >= from && i.OnDate < inclusiveTo && i.InvoiceStatus != InvoiceStatus.Cancelled);

        if (storeId.HasValue && storeId.Value != Guid.Empty)
        {
            invoiceQuery = invoiceQuery.Where(i => i.StoreId == storeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            invoiceQuery = invoiceQuery.Where(i =>
                i.InvoiceNumber.Contains(term) ||
                (i.CustomerName != null && i.CustomerName.Contains(term)) ||
                i.CustomerMobileNumber.Contains(term));
        }

        var invoices = await invoiceQuery
            .OrderByDescending(i => i.OnDate)
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.OnDate,
                CustomerName = i.CustomerName ?? "Walk-in Customer",
                i.CustomerMobileNumber,
                i.InterState,
                i.StoreId,
                StoreName = i.Store != null ? i.Store.Name : string.Empty
            })
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return new GstSaleReviewResponseDto(from, to, storeId, term, page, pageSize, 0, EmptySummary(), Array.Empty<GstSaleReviewInvoiceDto>(), Array.Empty<GstSaleReviewLineDto>());
        }

        var invoiceIds = invoices.Select(i => i.Id).ToHashSet();
        var invoiceMap = invoices.ToDictionary(i => i.Id);

        var items = await db.InvoiceItems.AsNoTracking()
            .Where(l => invoiceIds.Contains(l.InvoiceId))
            .Take(MaxLinesPerRequest)
            .ToListAsync(cancellationToken);

        var categoryIds = items.Where(l => l.ProductCategoryId.HasValue).Select(l => l.ProductCategoryId!.Value).Distinct().ToArray();
        var categoryNames = await db.ProductCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var findingCounts = await db.GstAuditFindings.AsNoTracking()
            .Where(f => !f.Deleted && f.Status == "Open" && f.ModuleArea == "Sale" && f.InvoiceId.HasValue && invoiceIds.Contains(f.InvoiceId.Value))
            .GroupBy(f => f.InvoiceId!.Value)
            .Select(g => new { InvoiceId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.InvoiceId, g => g.Count, cancellationToken);

        var lines = new List<GstSaleReviewLineDto>(items.Count);
        foreach (var item in items)
        {
            if (!invoiceMap.TryGetValue(item.InvoiceId, out var invoice))
            {
                continue;
            }

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

            lines.Add(new GstSaleReviewLineDto(
                invoice.Id,
                item.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                string.IsNullOrWhiteSpace(invoice.StoreName) ? "Store" : invoice.StoreName,
                invoice.CustomerName,
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
                l.CustomerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.Barcode.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var invoiceSummaries = lines
            .GroupBy(l => l.InvoiceId)
            .Select(g =>
            {
                var first = g.First();
                var issueCount = g.Count(l => l.Status != "OK");
                var sourceInvoice = invoiceMap[first.InvoiceId];
                return new GstSaleReviewInvoiceDto(
                    first.InvoiceId,
                    first.InvoiceNumber,
                    first.OnDate,
                    first.StoreName,
                    first.CustomerName,
                    sourceInvoice.CustomerMobileNumber,
                    sourceInvoice.InterState,
                    g.Count(),
                    issueCount,
                    findingCounts.GetValueOrDefault(first.InvoiceId),
                    g.Sum(l => l.StoredTaxAmount),
                    g.Sum(l => l.ResolvedTaxAmount),
                    g.Sum(l => l.TaxDifferenceAmount),
                    issueCount == 0 ? "OK" : "Needs Review");
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

        var summary = new GstSaleReviewSummaryDto(
            invoiceSummaries.Count,
            lines.Count,
            lines.Count(l => l.Status == "Rate Mismatch"),
            lines.Count(l => l.Status == "Not Configured"),
            invoiceSummaries.Sum(i => i.OpenAuditFindingCount),
            lines.Sum(l => l.TaxableValue),
            lines.Sum(l => l.StoredTaxAmount),
            lines.Sum(l => l.ResolvedTaxAmount),
            lines.Sum(l => l.TaxDifferenceAmount));

        return new GstSaleReviewResponseDto(from, to, storeId, term, page, pageSize, totalLines, summary, invoiceSummaries, pagedLines);
    }

    private static GstSaleReviewSummaryDto EmptySummary() => new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}
