using Garmetix.Api.Auth;
using Garmetix.Api.Database;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.GstTax;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GstTax;

/// <summary>
/// E-Invoice (IRN) and E-Way Bill placeholders (spec Stage GST-10). The Stage GST-1 provider registry was
/// already built provider-ready for this: GstTaxCatalog.ProviderTypes includes OfficialEInvoiceNIC/
/// OfficialEWayBill, and FeatureCodes includes the full EINVOICE_*/EWAYBILL_* set - but no adapter that
/// actually calls the government NIC e-invoice/e-way bill portals exists (a real integration, with signed
/// payload formats and portal-specific auth, is a substantially larger effort than this placeholder stage).
/// This module tracks lifecycle state (GstEinvoiceIrnRecord/GstEwaybillRecord) and gives an honest "not
/// configured"/"not yet implemented" message on every generate attempt, mirroring the exact precedent Stage
/// GST-2/3 already set for every non-Local-Master, non-GenericRestProvider provider type. Deliberately does not
/// implement Cancel here - cancelling a real IRN/e-way bill only makes sense once live generation exists, and a
/// cancel endpoint that can never act on a genuinely-generated record would be untestable dead code.
/// </summary>
public static class GstEinvoiceEwaybillEndpoints
{
    private const decimal EwayBillDefaultThreshold = 50000m;

    public static RouteGroupBuilder MapGstEinvoiceEwaybillEndpoints(this WebApplication app)
    {
        var einvoice = app.MapGroup("/api/gst/einvoice")
            .WithTags("GST & Taxes - E-Invoice")
            .RequireAuthorization(GarmetixPolicies.Gst);
        einvoice.MapGet("", GetEinvoiceListAsync);
        einvoice.MapPost("/generate/{invoiceId:guid}", GenerateEinvoiceAsync).RequireAuthorization(GarmetixPolicies.Edit);

        var ewaybill = app.MapGroup("/api/gst/ewaybill")
            .WithTags("GST & Taxes - E-Way Bill")
            .RequireAuthorization(GarmetixPolicies.Gst);
        ewaybill.MapGet("", GetEwaybillListAsync);
        ewaybill.MapPost("/generate/sale/{invoiceId:guid}", (Guid invoiceId, HttpContext context, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) =>
            GenerateEwaybillAsync("Sales", invoiceId, context, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Edit);
        ewaybill.MapPost("/generate/purchase/{purchaseInvoiceId:guid}", (Guid purchaseInvoiceId, HttpContext context, GarmetixDbContext db, ILoggerFactory lf, CancellationToken ct) =>
            GenerateEwaybillAsync("Purchase", purchaseInvoiceId, context, db, lf, ct)).RequireAuthorization(GarmetixPolicies.Edit);

        return einvoice;
    }

    private static async Task EnsureStorageAsync(GarmetixDbContext db, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        await DatabaseSchemaRepairService.RepairGstTaxStorageAsync(db, loggerFactory.CreateLogger("GstTaxStorageRepair"), cancellationToken);

    private static async Task<bool> IsFeatureConfiguredAsync(GarmetixDbContext db, string featureCode, CancellationToken cancellationToken)
    {
        var providerIds = await db.GstApiProviders.AsNoTracking()
            .Where(p => p.IsEnabled && p.ProviderType != "LocalMasterOnly")
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        if (providerIds.Count == 0)
        {
            return false;
        }

        return await db.GstApiProviderFeatures.AsNoTracking()
            .AnyAsync(f => providerIds.Contains(f.ProviderId) && f.FeatureCode == featureCode && f.IsEnabled, cancellationToken);
    }

    // --- E-Invoice ---

    private static async Task<GstEinvoiceListResponseDto> GetEinvoiceListAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? search = null,
        bool onlyPending = false,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var today = DateTime.Today;
        var from = (fromDate ?? today.AddDays(-30)).Date;
        var to = (toDate ?? today).Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }
        var inclusiveTo = to.AddDays(1);
        var term = search?.Trim();

        var providerConfigured = await IsFeatureConfiguredAsync(db, "EINVOICE_GENERATE_IRN", cancellationToken);

        var invoiceQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking().Where(i => !i.Deleted), context)
            .Where(i => i.OnDate >= from && i.OnDate < inclusiveTo && i.InvoiceStatus != InvoiceStatus.Cancelled && i.SaleInvoiceType == SaleInvoiceType.B2B);

        if (!string.IsNullOrWhiteSpace(term))
        {
            invoiceQuery = invoiceQuery.Where(i => i.InvoiceNumber.Contains(term) || (i.CustomerName != null && i.CustomerName.Contains(term)));
        }

        var invoices = await invoiceQuery
            .OrderByDescending(i => i.OnDate)
            .Select(i => new { i.Id, i.InvoiceNumber, i.OnDate, CustomerName = i.CustomerName ?? "Customer", i.CustomerGSTIN, i.SaleInvoiceType, i.BillAmount })
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(i => i.Id).ToArray();
        var records = await db.GstEinvoiceIrnRecords.AsNoTracking()
            .Where(r => !r.Deleted && invoiceIds.Contains(r.InvoiceId))
            .ToDictionaryAsync(r => r.InvoiceId, cancellationToken);

        var rows = invoices.Select(invoice =>
        {
            records.TryGetValue(invoice.Id, out var record);
            return new GstEinvoiceRowDto(
                invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerGSTIN,
                invoice.SaleInvoiceType.ToString(), invoice.BillAmount,
                record?.Status ?? "NotConfigured", record?.Irn, record?.AckNumber, record?.AckDate,
                record?.ErrorMessage, record?.LastAttemptAt);
        }).ToList();

        if (onlyPending)
        {
            rows = rows.Where(r => r.Status != "Generated").ToList();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);
        var totalRows = rows.Count;
        var pagedRows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new GstEinvoiceListResponseDto(
            from, to, term, onlyPending, page, pageSize, totalRows,
            rows.Count(r => r.Status != "Generated"), rows.Count(r => r.Status == "Generated"),
            providerConfigured, pagedRows);
    }

    private static async Task<GstEinvoiceGenerateResultDto> GenerateEinvoiceAsync(
        Guid invoiceId,
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking().Where(i => !i.Deleted), context)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new GstEinvoiceGenerateResultDto(invoiceId, "NotConfigured", "Sale invoice not found.", false);
        }

        var providerConfigured = await IsFeatureConfiguredAsync(db, "EINVOICE_GENERATE_IRN", cancellationToken);
        var message = providerConfigured
            ? "A GST API provider is configured for e-invoice generation, but live IRN generation against the government NIC portal is not implemented yet - this stage only tracks placeholder state."
            : "No GST API provider is configured for e-invoice generation. Set one up from GST API Setup with the EINVOICE_GENERATE_IRN feature enabled.";

        var record = await db.GstEinvoiceIrnRecords.FirstOrDefaultAsync(r => !r.Deleted && r.InvoiceId == invoiceId, cancellationToken);
        if (record is null)
        {
            record = new GstEinvoiceIrnRecord { InvoiceId = invoiceId, CompanyId = invoice.CompanyId };
            db.GstEinvoiceIrnRecords.Add(record);
        }

        record.Status = "NotConfigured";
        record.ErrorMessage = message;
        record.LastAttemptAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new GstEinvoiceGenerateResultDto(invoiceId, record.Status, message, providerConfigured);
    }

    // --- E-Way Bill ---

    private static async Task<GstEwaybillListResponseDto> GetEwaybillListAsync(
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? direction = null,
        string? search = null,
        bool onlyPending = false,
        int page = 1,
        int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        var today = DateTime.Today;
        var from = (fromDate ?? today.AddDays(-30)).Date;
        var to = (toDate ?? today).Date;
        if (to < from)
        {
            (from, to) = (to, from);
        }
        var inclusiveTo = to.AddDays(1);
        var term = search?.Trim();
        var normalizedDirection = string.IsNullOrWhiteSpace(direction) ? "both" : direction.Trim().ToLowerInvariant();

        var providerConfigured = await IsFeatureConfiguredAsync(db, "EWAYBILL_GENERATE", cancellationToken);
        var rows = new List<GstEwaybillRowDto>();

        if (normalizedDirection is "sales" or "both")
        {
            var saleQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking().Where(i => !i.Deleted), context)
                .Where(i => i.OnDate >= from && i.OnDate < inclusiveTo && i.InvoiceStatus != InvoiceStatus.Cancelled);
            if (!string.IsNullOrWhiteSpace(term))
            {
                saleQuery = saleQuery.Where(i => i.InvoiceNumber.Contains(term) || (i.CustomerName != null && i.CustomerName.Contains(term)));
            }
            var sales = await saleQuery.OrderByDescending(i => i.OnDate)
                .Select(i => new { i.Id, i.InvoiceNumber, i.OnDate, CustomerName = i.CustomerName ?? "Customer", i.CustomerGSTIN, i.BillAmount })
                .ToListAsync(cancellationToken);
            var saleIds = sales.Select(i => i.Id).ToArray();
            var saleRecords = await db.GstEwaybillRecords.AsNoTracking()
                .Where(r => !r.Deleted && r.InvoiceId != null && saleIds.Contains(r.InvoiceId.Value))
                .ToDictionaryAsync(r => r.InvoiceId!.Value, cancellationToken);
            rows.AddRange(sales.Select(invoice =>
            {
                saleRecords.TryGetValue(invoice.Id, out var record);
                return new GstEwaybillRowDto("Sales", invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.CustomerName, invoice.CustomerGSTIN,
                    invoice.BillAmount, invoice.BillAmount > EwayBillDefaultThreshold,
                    record?.Status ?? "NotConfigured", record?.EwbNumber, record?.EwbDate, record?.ValidUpto, record?.ErrorMessage, record?.LastAttemptAt);
            }));
        }

        if (normalizedDirection is "purchase" or "both")
        {
            var purchaseQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking().Where(i => !i.Deleted), context)
                .Where(i => i.OnDate >= from && i.OnDate < inclusiveTo && i.InvoiceStatus != InvoiceStatus.Cancelled);
            if (!string.IsNullOrWhiteSpace(term))
            {
                purchaseQuery = purchaseQuery.Where(i => i.InvoiceNumber.Contains(term) || (i.VendorName != null && i.VendorName.Contains(term)));
            }
            var purchases = await purchaseQuery.OrderByDescending(i => i.OnDate)
                .Select(i => new { i.Id, i.InvoiceNumber, i.OnDate, VendorName = i.VendorName ?? "Vendor", i.VendorGSTIN, i.BillAmount })
                .ToListAsync(cancellationToken);
            var purchaseIds = purchases.Select(i => i.Id).ToArray();
            var purchaseRecords = await db.GstEwaybillRecords.AsNoTracking()
                .Where(r => !r.Deleted && r.PurchaseInvoiceId != null && purchaseIds.Contains(r.PurchaseInvoiceId.Value))
                .ToDictionaryAsync(r => r.PurchaseInvoiceId!.Value, cancellationToken);
            rows.AddRange(purchases.Select(invoice =>
            {
                purchaseRecords.TryGetValue(invoice.Id, out var record);
                return new GstEwaybillRowDto("Purchase", invoice.Id, invoice.InvoiceNumber, invoice.OnDate, invoice.VendorName, invoice.VendorGSTIN,
                    invoice.BillAmount, invoice.BillAmount > EwayBillDefaultThreshold,
                    record?.Status ?? "NotConfigured", record?.EwbNumber, record?.EwbDate, record?.ValidUpto, record?.ErrorMessage, record?.LastAttemptAt);
            }));
        }

        if (onlyPending)
        {
            rows = rows.Where(r => r.IsAboveThreshold && r.Status != "Generated").ToList();
        }

        var ordered = rows.OrderByDescending(r => r.OnDate).ThenBy(r => r.InvoiceNumber).ToList();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 500);
        var totalRows = ordered.Count;
        var pagedRows = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new GstEwaybillListResponseDto(
            from, to, normalizedDirection, term, onlyPending, page, pageSize, totalRows,
            ordered.Count(r => r.IsAboveThreshold && r.Status != "Generated"), ordered.Count(r => r.Status == "Generated"),
            providerConfigured, pagedRows);
    }

    private static async Task<GstEwaybillGenerateResultDto> GenerateEwaybillAsync(
        string direction,
        Guid invoiceId,
        HttpContext context,
        GarmetixDbContext db,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        await EnsureStorageAsync(db, loggerFactory, cancellationToken);

        Guid companyId;
        if (direction == "Sales")
        {
            var invoice = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking().Where(i => !i.Deleted), context)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
            if (invoice is null)
            {
                return new GstEwaybillGenerateResultDto(direction, invoiceId, "NotConfigured", "Sale invoice not found.", false);
            }
            companyId = invoice.CompanyId;
        }
        else
        {
            var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking().Where(i => !i.Deleted), context)
                .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);
            if (invoice is null)
            {
                return new GstEwaybillGenerateResultDto(direction, invoiceId, "NotConfigured", "Purchase invoice not found.", false);
            }
            companyId = invoice.CompanyId;
        }

        var providerConfigured = await IsFeatureConfiguredAsync(db, "EWAYBILL_GENERATE", cancellationToken);
        var message = providerConfigured
            ? "A GST API provider is configured for e-way bill generation, but live generation against the government e-way bill portal is not implemented yet - this stage only tracks placeholder state."
            : "No GST API provider is configured for e-way bill generation. Set one up from GST API Setup with the EWAYBILL_GENERATE feature enabled.";

        var record = direction == "Sales"
            ? await db.GstEwaybillRecords.FirstOrDefaultAsync(r => !r.Deleted && r.InvoiceId == invoiceId, cancellationToken)
            : await db.GstEwaybillRecords.FirstOrDefaultAsync(r => !r.Deleted && r.PurchaseInvoiceId == invoiceId, cancellationToken);
        if (record is null)
        {
            record = new GstEwaybillRecord { CompanyId = companyId };
            if (direction == "Sales") record.InvoiceId = invoiceId; else record.PurchaseInvoiceId = invoiceId;
            db.GstEwaybillRecords.Add(record);
        }

        record.Status = "NotConfigured";
        record.ErrorMessage = message;
        record.LastAttemptAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new GstEwaybillGenerateResultDto(direction, invoiceId, record.Status, message, providerConfigured);
    }
}
