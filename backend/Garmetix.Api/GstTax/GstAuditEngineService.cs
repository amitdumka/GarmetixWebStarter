using System.Text.RegularExpressions;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.GstTax;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// The configurable GST audit engine (spec section 11): runs the enabled rules from the Stage GST-1 seed
/// catalog against Product/Customer/Vendor masters and Sale/Purchase invoices, writing gst_audit_findings.
/// A run over a large date range can touch a lot of lines - capped at 2000 lines per module per run so an
/// unscoped "audit everything" click can't run away; narrow the date range for a full historical sweep.
/// </summary>
public sealed class GstAuditEngineService(GarmetixDbContext db, GstRateResolutionService rateResolver)
{
    private const int MaxLinesPerRun = 2000;
    private static readonly Regex GstinFormatPattern = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<GstAuditRunResultDto> RunAsync(HttpContext context, string? moduleArea, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var rules = await db.GstAuditRules.AsNoTracking().Where(r => r.IsEnabled).ToListAsync(cancellationToken);
        var rulesByCode = rules.ToDictionary(r => r.RuleCode, StringComparer.OrdinalIgnoreCase);
        var notes = new List<string>();
        var created = 0;
        var scanned = 0;

        var companyId = WorkspaceScope.ClaimGuid(context, "companyId");
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        if (moduleArea is null or "Product")
        {
            var (c, s) = await RunProductChecksAsync(context, rulesByCode, companyId, cancellationToken);
            created += c;
            scanned += s;
        }

        if (moduleArea is null or "Customer")
        {
            var (c, s) = await RunPartyChecksAsync(context, rulesByCode, isVendor: false, companyId, cancellationToken);
            created += c;
            scanned += s;
        }

        if (moduleArea is null or "Vendor")
        {
            var (c, s) = await RunPartyChecksAsync(context, rulesByCode, isVendor: true, companyId, cancellationToken);
            created += c;
            scanned += s;
        }

        if (moduleArea is null or "Sale")
        {
            var (c, s) = await RunSaleChecksAsync(context, rulesByCode, companyId, from, to, cancellationToken);
            created += c;
            scanned += s;
        }

        if (moduleArea is null or "Purchase")
        {
            var (c, s) = await RunPurchaseChecksAsync(context, rulesByCode, companyId, from, to, cancellationToken);
            created += c;
            scanned += s;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new GstAuditRunResultDto(created, scanned, notes);
    }

    private async Task<(int Created, int Scanned)> RunProductChecksAsync(
        HttpContext context,
        Dictionary<string, GstAuditRule> rules,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var products = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking().Where(p => !p.Deleted), context).ToListAsync(cancellationToken);
        var hsnCodes = products.Where(p => !string.IsNullOrWhiteSpace(p.HSNCode)).Select(p => p.HSNCode!).Distinct().ToArray();
        var hsnMasters = await db.GstHsnMasters.AsNoTracking().Where(h => hsnCodes.Contains(h.HsnCode)).ToDictionaryAsync(h => h.HsnCode, cancellationToken);

        var created = 0;
        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.HSNCode))
            {
                created += await RaiseAsync(rules, "HSN_MISSING", "Product", product.Id, companyId, cancellationToken,
                    message: $"Product '{product.Name}' has no HSN/SAC code.");
                continue;
            }

            if (!hsnMasters.TryGetValue(product.HSNCode, out var hsn))
            {
                created += await RaiseAsync(rules, "HSN_INVALID", "Product", product.Id, companyId, cancellationToken,
                    message: $"Product '{product.Name}' uses HSN '{product.HSNCode}', which is not in the local HSN/SAC master.",
                    actualValue: product.HSNCode);
            }
            else if (string.IsNullOrWhiteSpace(hsn.DefaultUqc))
            {
                created += await RaiseAsync(rules, "UQC_MISSING", "Product", product.Id, companyId, cancellationToken,
                    message: $"HSN '{product.HSNCode}' (used by '{product.Name}') has no default Unit Quantity Code set.",
                    hsnCode: product.HSNCode);
            }

            if (product.TaxRate <= 0)
            {
                created += await RaiseAsync(rules, "GST_RATE_MISSING", "Product", product.Id, companyId, cancellationToken,
                    message: $"Product '{product.Name}' has no usable GST rate configured.",
                    hsnCode: product.HSNCode);
                continue;
            }

            var resolved = await rateResolver.ResolveAsync(new GstRateResolveRequest(product.HSNCode, null, product.MRP, DateTime.UtcNow, true), cancellationToken);
            if (resolved.Success && Math.Abs(resolved.TaxRate - product.TaxRate) > 0.01m)
            {
                created += await RaiseAsync(rules, "GST_RATE_MISMATCH", "Product", product.Id, companyId, cancellationToken,
                    message: $"Product '{product.Name}' is set to {product.TaxRate}% GST, but the Rate Master resolves {resolved.TaxRate}% ({resolved.RuleName}) for its HSN/price.",
                    hsnCode: product.HSNCode,
                    expectedValue: resolved.TaxRate.ToString("0.###"),
                    actualValue: product.TaxRate.ToString("0.###"));
            }
        }

        return (created, products.Count);
    }

    private async Task<(int Created, int Scanned)> RunPartyChecksAsync(
        HttpContext context,
        Dictionary<string, GstAuditRule> rules,
        bool isVendor,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        var created = 0;
        var scanned = 0;

        if (isVendor)
        {
            var vendors = await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking().Where(v => !v.Deleted), context).ToListAsync(cancellationToken);
            scanned = vendors.Count;
            foreach (var vendor in vendors)
            {
                created += await RunSinglePartyChecksAsync(rules, "Vendor", vendor.Id, vendor.Name, vendor.GSTIN, vendor.GSTVerified, vendor.GSTRegistrationStatus, null, companyId, cancellationToken);
            }
        }
        else
        {
            var customers = await WorkspaceScope.ApplyTo(db.Customers.AsNoTracking().Where(c => !c.Deleted), context).ToListAsync(cancellationToken);
            scanned = customers.Count;
            foreach (var customer in customers)
            {
                created += await RunSinglePartyChecksAsync(rules, "Customer", customer.Id, customer.Name, customer.GSTIN, customer.GSTVerified, customer.GSTRegistrationStatus, customer.State, companyId, cancellationToken);
            }
        }

        return (created, scanned);
    }

    private async Task<int> RunSinglePartyChecksAsync(
        Dictionary<string, GstAuditRule> rules,
        string moduleArea,
        Guid entityId,
        string partyName,
        string? gstin,
        bool gstVerified,
        string? registrationStatus,
        string? declaredState,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gstin))
        {
            return 0;
        }

        var created = 0;
        var normalized = gstin.Trim().ToUpperInvariant();

        if (!GstinFormatPattern.IsMatch(normalized))
        {
            created += await RaiseAsync(rules, "GSTIN_FORMAT_INVALID", moduleArea, entityId, companyId, cancellationToken,
                message: $"{moduleArea} '{partyName}' has a GSTIN that does not match the required format.",
                gstin: normalized);
            return created;
        }

        if (gstVerified && !string.IsNullOrWhiteSpace(registrationStatus) && !registrationStatus.Contains("active", StringComparison.OrdinalIgnoreCase))
        {
            created += await RaiseAsync(rules, "GSTIN_INACTIVE", moduleArea, entityId, companyId, cancellationToken,
                message: $"{moduleArea} '{partyName}' GSTIN shows registration status '{registrationStatus}', not Active.",
                gstin: normalized,
                actualValue: registrationStatus);
        }

        if (!string.IsNullOrWhiteSpace(declaredState))
        {
            var gstinStateCode = normalized[..2];
            var stateNameForCode = await db.GstStateCodes.AsNoTracking().Where(s => s.StateCode == gstinStateCode).Select(s => s.StateName).FirstOrDefaultAsync(cancellationToken);
            if (stateNameForCode is not null && !string.Equals(stateNameForCode, declaredState, StringComparison.OrdinalIgnoreCase)
                && !stateNameForCode.Contains(declaredState, StringComparison.OrdinalIgnoreCase) && !declaredState.Contains(stateNameForCode, StringComparison.OrdinalIgnoreCase))
            {
                created += await RaiseAsync(rules, "GSTIN_STATE_MISMATCH", moduleArea, entityId, companyId, cancellationToken,
                    message: $"{moduleArea} '{partyName}' is recorded in '{declaredState}', but the GSTIN's state code corresponds to '{stateNameForCode}'.",
                    gstin: normalized,
                    expectedValue: declaredState,
                    actualValue: stateNameForCode);
            }
        }

        return created;
    }

    private async Task<(int Created, int Scanned)> RunSaleChecksAsync(
        HttpContext context,
        Dictionary<string, GstAuditRule> rules,
        Guid? companyId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking().Where(i => !i.Deleted && i.OnDate >= from && i.OnDate <= to), context)
            .OrderByDescending(i => i.OnDate)
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var invoice in invoices)
        {
            created += RaiseHeaderTaxSplitFindings(rules, "Sale", invoice.Id, invoice.InterState, invoice.CGSTAmount, invoice.SGSTAmount, invoice.IGSTAmount, companyId, invoiceId: invoice.Id);

            if (invoice.SaleInvoiceType == SaleInvoiceType.B2B && string.IsNullOrWhiteSpace(invoice.CustomerGSTIN))
            {
                created += await RaiseAsync(rules, "B2B_GSTIN_REQUIRED", "Sale", invoice.Id, companyId, cancellationToken,
                    message: $"B2B sale invoice '{invoice.InvoiceNumber}' has no customer GSTIN recorded.",
                    invoiceId: invoice.Id);
            }
        }

        var invoiceIds = invoices.Select(i => i.Id).ToArray();
        var lines = await db.InvoiceItems.AsNoTracking()
            .Where(l => invoiceIds.Contains(l.InvoiceId))
            .Take(MaxLinesPerRun)
            .ToListAsync(cancellationToken);
        var invoicesById = invoices.ToDictionary(i => i.Id);

        var lineCreated = await RunLineChecksAsync(rules, "Sale", "SALE_LINE_TAX_MISMATCH", lines, companyId,
            line => invoicesById.TryGetValue(line.InvoiceId, out var inv) ? (inv.OnDate, !inv.InterState) : (DateTime.UtcNow, true),
            line => line.InvoiceId,
            cancellationToken);
        created += lineCreated;

        var headerVsLines = await RaiseInvoiceTotalMismatchFindingsAsync(rules, "Sale", invoices.Select(i => (i.Id, i.CGSTAmount, i.SGSTAmount, i.IGSTAmount, i.TaxAmount)),
            lines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.Sum(l => l.TaxAmount)), companyId, cancellationToken);
        created += headerVsLines;

        return (created, invoices.Count);
    }

    private async Task<(int Created, int Scanned)> RunPurchaseChecksAsync(
        HttpContext context,
        Dictionary<string, GstAuditRule> rules,
        Guid? companyId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking().Where(i => !i.Deleted && i.OnDate >= from && i.OnDate <= to), context)
            .OrderByDescending(i => i.OnDate)
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var invoice in invoices)
        {
            created += RaiseHeaderTaxSplitFindings(rules, "Purchase", invoice.Id, invoice.InterState, invoice.CGSTAmount, invoice.SGSTAmount, invoice.IGSTAmount, companyId, purchaseInvoiceId: invoice.Id);
        }

        var invoiceIds = invoices.Select(i => i.Id).ToArray();
        var lines = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(l => invoiceIds.Contains(l.InvoiceId))
            .Take(MaxLinesPerRun)
            .ToListAsync(cancellationToken);
        var invoicesById = invoices.ToDictionary(i => i.Id);

        created += await RunLineChecksAsync(rules, "Purchase", "PURCHASE_LINE_TAX_MISMATCH", lines, companyId,
            line => invoicesById.TryGetValue(line.InvoiceId, out var inv) ? (inv.OnDate, !inv.InterState) : (DateTime.UtcNow, true),
            line => line.InvoiceId,
            cancellationToken, isPurchase: true);

        created += await RaiseInvoiceTotalMismatchFindingsAsync(rules, "Purchase", invoices.Select(i => (i.Id, i.CGSTAmount, i.SGSTAmount, i.IGSTAmount, i.TaxAmount)),
            lines.GroupBy(l => l.InvoiceId).ToDictionary(g => g.Key, g => g.Sum(l => l.TaxAmount)), companyId, cancellationToken, isPurchase: true);

        return (created, invoices.Count);
    }

    private int RaiseHeaderTaxSplitFindings(
        Dictionary<string, GstAuditRule> rules,
        string moduleArea,
        Guid invoiceRowId,
        bool interState,
        decimal? cgst,
        decimal? sgst,
        decimal? igst,
        Guid? companyId,
        Guid? invoiceId = null,
        Guid? purchaseInvoiceId = null)
    {
        var created = 0;
        const decimal tolerance = 1m;

        if (!interState)
        {
            if ((igst ?? 0m) > tolerance)
            {
                created += RaiseSync(rules, "INTRASTATE_IGST_USED", moduleArea, invoiceRowId, companyId,
                    $"Intra-state invoice has IGST of {igst}.", invoiceId, purchaseInvoiceId);
            }

            if (cgst.HasValue && sgst.HasValue && Math.Abs(cgst.Value - sgst.Value) > tolerance)
            {
                created += RaiseSync(rules, "CGST_SGST_NOT_EQUAL", moduleArea, invoiceRowId, companyId,
                    $"CGST ({cgst}) and SGST ({sgst}) are not equal on an intra-state invoice.", invoiceId, purchaseInvoiceId);
            }
        }
        else if ((cgst ?? 0m) > tolerance || (sgst ?? 0m) > tolerance)
        {
            created += RaiseSync(rules, "INTERSTATE_CGST_SGST_USED", moduleArea, invoiceRowId, companyId,
                $"Inter-state invoice has CGST ({cgst}) / SGST ({sgst}) instead of IGST.", invoiceId, purchaseInvoiceId);
        }

        return created;
    }

    private async Task<int> RunLineChecksAsync(
        Dictionary<string, GstAuditRule> rules,
        string moduleArea,
        string mismatchRuleCode,
        IEnumerable<InvoiceItem> lines,
        Guid? companyId,
        Func<InvoiceItem, (DateTime OnDate, bool IsIntraState)> invoiceContext,
        Func<InvoiceItem, Guid> invoiceIdSelector,
        CancellationToken cancellationToken,
        bool isPurchase = false)
    {
        var created = 0;
        var categoryIds = lines.Where(l => l.ProductCategoryId.HasValue).Select(l => l.ProductCategoryId!.Value).Distinct().ToArray();
        var categoryNames = await db.ProductCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.HSNCode))
            {
                created += await RaiseAsync(rules, "HSN_MISSING", moduleArea, line.Id, companyId, cancellationToken,
                    message: "Invoice line has no HSN/SAC code.",
                    invoiceId: isPurchase ? null : invoiceIdSelector(line),
                    purchaseInvoiceId: isPurchase ? invoiceIdSelector(line) : null,
                    lineId: line.Id);
                continue;
            }

            var taxableValue = Math.Round((line.MRP - line.DiscountAmount) / (1 + line.TaxPercentage / 100m), 2);
            var expectedTax = Math.Round(taxableValue * (line.TaxPercentage / 100m), 2);
            if (Math.Abs(expectedTax - line.TaxAmount) > 1m)
            {
                created += await RaiseAsync(rules, mismatchRuleCode, moduleArea, line.Id, companyId, cancellationToken,
                    message: $"Line for barcode '{line.Barcode}' expects tax {expectedTax} at {line.TaxPercentage}%, but {line.TaxAmount} is stored.",
                    hsnCode: line.HSNCode,
                    expectedValue: expectedTax.ToString("0.##"),
                    actualValue: line.TaxAmount.ToString("0.##"),
                    invoiceId: isPurchase ? null : invoiceIdSelector(line),
                    purchaseInvoiceId: isPurchase ? invoiceIdSelector(line) : null,
                    lineId: line.Id);
            }

            var (onDate, isIntraState) = invoiceContext(line);
            var categoryName = line.ProductCategoryId.HasValue ? categoryNames.GetValueOrDefault(line.ProductCategoryId.Value) : null;
            var resolved = await rateResolver.ResolveAsync(new GstRateResolveRequest(line.HSNCode, categoryName, line.MRP - line.DiscountAmount, onDate, isIntraState), cancellationToken);
            if (resolved.Success && Math.Abs(resolved.TaxRate - line.TaxPercentage) > 0.01m)
            {
                var isGarmentThreshold = string.Equals(categoryName, "Garments", StringComparison.OrdinalIgnoreCase) && resolved.Source == "Rate Master";
                created += await RaiseAsync(rules, isGarmentThreshold ? "GARMENT_THRESHOLD_RATE_MISMATCH" : "GST_RATE_MISMATCH", moduleArea, line.Id, companyId, cancellationToken,
                    message: $"Line for barcode '{line.Barcode}' is billed at {line.TaxPercentage}%, but the Rate Master resolves {resolved.TaxRate}% ({resolved.RuleName}).",
                    hsnCode: line.HSNCode,
                    expectedValue: resolved.TaxRate.ToString("0.###"),
                    actualValue: line.TaxPercentage.ToString("0.###"),
                    invoiceId: isPurchase ? null : invoiceIdSelector(line),
                    purchaseInvoiceId: isPurchase ? invoiceIdSelector(line) : null,
                    lineId: line.Id);
            }
        }

        return created;
    }

    private async Task<int> RaiseInvoiceTotalMismatchFindingsAsync(
        Dictionary<string, GstAuditRule> rules,
        string moduleArea,
        IEnumerable<(Guid Id, decimal? Cgst, decimal? Sgst, decimal? Igst, decimal TaxAmount)> invoices,
        Dictionary<Guid, decimal> lineTaxTotalsByInvoice,
        Guid? companyId,
        CancellationToken cancellationToken,
        bool isPurchase = false)
    {
        var created = 0;
        foreach (var invoice in invoices)
        {
            if (!lineTaxTotalsByInvoice.TryGetValue(invoice.Id, out var lineTotal))
            {
                continue;
            }

            var headerTotal = (invoice.Cgst ?? 0m) + (invoice.Sgst ?? 0m) + (invoice.Igst ?? 0m);
            if (headerTotal <= 0m)
            {
                headerTotal = invoice.TaxAmount;
            }

            if (Math.Abs(headerTotal - lineTotal) > 2m)
            {
                created += await RaiseAsync(rules, "INVOICE_TOTAL_TAX_MISMATCH", moduleArea, invoice.Id, companyId, cancellationToken,
                    message: $"Invoice total tax ({headerTotal}) does not match the sum of its line taxes ({lineTotal}).",
                    expectedValue: lineTotal.ToString("0.##"),
                    actualValue: headerTotal.ToString("0.##"),
                    invoiceId: isPurchase ? null : invoice.Id,
                    purchaseInvoiceId: isPurchase ? invoice.Id : null);
            }
        }

        return created;
    }

    private int RaiseSync(
        Dictionary<string, GstAuditRule> rules,
        string ruleCode,
        string moduleArea,
        Guid entityId,
        Guid? companyId,
        string message,
        Guid? invoiceId,
        Guid? purchaseInvoiceId)
    {
        if (!rules.TryGetValue(ruleCode, out var rule))
        {
            return 0;
        }

        db.GstAuditFindings.Add(new GstAuditFinding
        {
            CompanyId = companyId ?? Guid.Empty,
            RuleCode = ruleCode,
            Severity = rule.Severity,
            ModuleArea = moduleArea,
            EntityType = moduleArea,
            EntityId = entityId,
            InvoiceId = invoiceId,
            PurchaseInvoiceId = purchaseInvoiceId,
            Message = message,
            Status = "Open"
        });

        return 1;
    }

    private async Task<int> RaiseAsync(
        Dictionary<string, GstAuditRule> rules,
        string ruleCode,
        string moduleArea,
        Guid entityId,
        Guid? companyId,
        CancellationToken cancellationToken,
        string message,
        string? gstin = null,
        string? hsnCode = null,
        string? expectedValue = null,
        string? actualValue = null,
        Guid? invoiceId = null,
        Guid? purchaseInvoiceId = null,
        Guid? lineId = null)
    {
        if (!rules.TryGetValue(ruleCode, out var rule))
        {
            return 0;
        }

        var alreadyOpen = await db.GstAuditFindings.AnyAsync(f =>
            f.RuleCode == ruleCode && f.EntityType == moduleArea && f.EntityId == entityId && f.Status == "Open", cancellationToken);
        if (alreadyOpen)
        {
            return 0;
        }

        db.GstAuditFindings.Add(new GstAuditFinding
        {
            CompanyId = companyId ?? Guid.Empty,
            RuleCode = ruleCode,
            Severity = rule.Severity,
            ModuleArea = moduleArea,
            EntityType = moduleArea,
            EntityId = entityId,
            Gstin = gstin,
            HsnCode = hsnCode,
            Message = message,
            ExpectedValue = expectedValue,
            ActualValue = actualValue,
            InvoiceId = invoiceId,
            PurchaseInvoiceId = purchaseInvoiceId,
            LineId = lineId,
            Status = "Open"
        });

        return 1;
    }
}
