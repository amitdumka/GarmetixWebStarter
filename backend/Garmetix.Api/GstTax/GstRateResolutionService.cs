using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// Resolves the GST rate for a sale/purchase line from the date-effective Rate Master (spec section 9):
/// match HSN first, then product category, then apply the garment/category price-threshold slab, honoring
/// rule priority. Uses the invoice date, not "now" - a rate change must never retroactively alter an
/// already-posted historical invoice line. Falls back to the HSN master's own default rate, then gives an
/// honest "nothing configured" answer rather than guessing - callers decide whether that's a warning or a block.
/// </summary>
public sealed class GstRateResolutionService(GarmetixDbContext db)
{
    public async Task<GstRateResolveResult> ResolveAsync(GstRateResolveRequest request, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var invoiceDate = request.InvoiceDate == default ? DateTime.UtcNow.Date : request.InvoiceDate.Date;

        var activeRules = await db.GstTaxRateRules.AsNoTracking()
            .Where(r => r.IsActive && r.EffectiveFrom <= invoiceDate && (r.EffectiveTo == null || r.EffectiveTo >= invoiceDate))
            .ToListAsync(cancellationToken);

        var hsnCode = string.IsNullOrWhiteSpace(request.HsnCode) ? null : request.HsnCode.Trim();
        var category = string.IsNullOrWhiteSpace(request.ProductCategory) ? null : request.ProductCategory.Trim();

        var candidates = hsnCode is not null
            ? activeRules.Where(r => string.Equals(r.HsnCode, hsnCode, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];

        if (candidates.Count == 0 && category is not null)
        {
            candidates = activeRules
                .Where(r => r.HsnCode is null && string.Equals(r.ProductCategory, category, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (candidates.Count > 1)
        {
            candidates = candidates.Where(r => WithinThreshold(r.PriceThresholdFrom, r.PriceThresholdTo, request.BasicRateAfterDiscount)).ToList();
        }
        else if (candidates.Count == 1 && !WithinThreshold(candidates[0].PriceThresholdFrom, candidates[0].PriceThresholdTo, request.BasicRateAfterDiscount))
        {
            candidates = [];
        }

        var matched = candidates.OrderBy(r => r.Priority).FirstOrDefault();

        if (matched is not null)
        {
            var (cgst, sgst, igst) = SplitRate(matched.TaxRate, matched.CgstRate, matched.SgstRate, matched.IgstRate, request.IsIntraState);
            return new GstRateResolveResult(true, matched.TaxRate, cgst, sgst, igst, matched.CessRate ?? 0m, matched.RuleName, "Rate Master", warnings);
        }

        if (hsnCode is not null)
        {
            var hsn = await db.GstHsnMasters.AsNoTracking().FirstOrDefaultAsync(h => h.HsnCode == hsnCode, cancellationToken);
            if (hsn?.DefaultGstRate is { } defaultRate)
            {
                warnings.Add("No specific rate rule matched for this HSN/category/price combination - falling back to the HSN master's default GST rate.");
                var (cgst, sgst, igst) = SplitRate(defaultRate, hsn.CgstRate, hsn.SgstRate, hsn.IgstRate, request.IsIntraState);
                return new GstRateResolveResult(true, defaultRate, cgst, sgst, igst, hsn.CessRate ?? 0m, "HSN Master Default", "HSN Master", warnings);
            }
        }

        warnings.Add("No rate rule or HSN default GST rate found for this combination. Configure one in GST & Taxes -> Rate Master or HSN/SAC Master.");
        return new GstRateResolveResult(false, 0m, 0m, 0m, 0m, 0m, null, null, warnings);
    }

    private static bool WithinThreshold(decimal? from, decimal? to, decimal value) =>
        (from is null || value >= from) && (to is null || value <= to);

    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitRate(decimal taxRate, decimal? cgst, decimal? sgst, decimal? igst, bool isIntraState)
    {
        if (cgst.HasValue || sgst.HasValue || igst.HasValue)
        {
            return (cgst ?? 0m, sgst ?? 0m, igst ?? 0m);
        }

        return isIntraState ? (taxRate / 2m, taxRate / 2m, 0m) : (0m, 0m, taxRate);
    }
}
