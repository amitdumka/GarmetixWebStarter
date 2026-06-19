using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Production;

public sealed record PrintAcceptanceDocumentDto(
    string Key,
    string Label,
    string Area,
    int Count,
    string? LatestNumber,
    Guid? LatestId,
    DateTime? LatestDate,
    string Status,
    string? Endpoint,
    string Message);

public sealed record PrintAcceptanceStatusDto(
    DateTimeOffset CheckedAtUtc,
    int ReadyCount,
    int TotalCount,
    IReadOnlyList<PrintAcceptanceDocumentDto> Documents,
    IReadOnlyList<string> Recommendations);

public static class PrintAcceptanceEndpoints
{
    public static RouteGroupBuilder MapPrintAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/print-acceptance")
            .WithTags("Print Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", StatusAsync);

        return group;
    }

    private static async Task<PrintAcceptanceStatusDto> StatusAsync(
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var docs = new List<PrintAcceptanceDocumentDto>();

        var voucherRows = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.VoucherNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "voucher", "Voucher PDF", "Accounting", voucherRows.Count, voucherRows.FirstOrDefault()?.Number, voucherRows.FirstOrDefault()?.Id, voucherRows.FirstOrDefault()?.Date, voucherRows.FirstOrDefault()?.Id is Guid voucherId ? $"/api/vouchers/{voucherId}/pdf" : null);

        var cashVoucherRows = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.VoucherNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "cashVoucher", "Cash Voucher PDF", "Off Book", cashVoucherRows.Count, cashVoucherRows.FirstOrDefault()?.Number, cashVoucherRows.FirstOrDefault()?.Id, cashVoucherRows.FirstOrDefault()?.Date, cashVoucherRows.FirstOrDefault()?.Id is Guid cashVoucherId ? $"/api/cash-vouchers/{cashVoucherId}/pdf" : null);

        var pettyRows = await db.PettyCashSheets.AsNoTracking()
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.OnDate.ToString("yyyy-MM-dd"), Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "pettyCash", "Petty Cash Sheet PDF", "Accounting", pettyRows.Count, pettyRows.FirstOrDefault()?.Number, pettyRows.FirstOrDefault()?.Id, pettyRows.FirstOrDefault()?.Date, pettyRows.FirstOrDefault()?.Id is Guid pettyId ? $"/api/petty-cash-sheets/{pettyId}/pdf" : null);

        var purchaseRows = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .OrderByDescending(item => item.InwardDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.InwardNumber, Date = item.InwardDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "purchaseInward", "Purchase Inward PDF", "Purchase", purchaseRows.Count, purchaseRows.FirstOrDefault()?.Number, purchaseRows.FirstOrDefault()?.Id, purchaseRows.FirstOrDefault()?.Date, purchaseRows.FirstOrDefault()?.Id is Guid purchaseId ? $"/api/purchase/invoices/{purchaseId}/pdf" : null);

        var tailoringRows = await WorkspaceScope.ApplyTo(db.TailoringOrders.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, Number = item.OrderNumber, Date = item.OnDate })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "tailoringOrder", "Tailoring Order / Invoice Print", "Tailoring", tailoringRows.Count, tailoringRows.FirstOrDefault()?.Number, tailoringRows.FirstOrDefault()?.Id, tailoringRows.FirstOrDefault()?.Date, tailoringRows.FirstOrDefault()?.Id is Guid tailoringId ? $"/api/tailoring/orders/{tailoringId}/print-order" : null);

        var gstrRows = await WorkspaceScope.ApplyTo(db.GstReturnDrafts.AsNoTracking(), context)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new { item.Id, Number = item.Form.ToUpper() + " " + item.ReturnPeriod, Date = item.UpdatedAt ?? item.CreatedAt })
            .Take(1)
            .ToListAsync(cancellationToken);
        AddDocument(docs, "gstReturn", "GST Return Export / CA Review", "GST", gstrRows.Count, gstrRows.FirstOrDefault()?.Number, gstrRows.FirstOrDefault()?.Id, gstrRows.FirstOrDefault()?.Date, gstrRows.FirstOrDefault()?.Id is Guid gstId ? $"/api/gst-returns/drafts/{gstId}/excel" : null);

        var readyCount = docs.Count(item => string.Equals(item.Status, "Ready", StringComparison.OrdinalIgnoreCase));
        var recommendations = new List<string>();
        if (readyCount < docs.Count)
        {
            recommendations.Add("Create at least one sample record in each missing area, then run print acceptance again.");
        }
        recommendations.Add("Open each document from its source page and verify logo, date, amount, GST/tax, signatures and page size.");
        recommendations.Add("Use browser print/PDF download once from the live Mac mini URL before production handover.");

        return new PrintAcceptanceStatusDto(
            DateTimeOffset.UtcNow,
            readyCount,
            docs.Count,
            docs,
            recommendations);
    }

    private static void AddDocument(
        List<PrintAcceptanceDocumentDto> docs,
        string key,
        string label,
        string area,
        int count,
        string? latestNumber,
        Guid? latestId,
        DateTime? latestDate,
        string? endpoint)
    {
        var ready = latestId.HasValue && count > 0;
        docs.Add(new PrintAcceptanceDocumentDto(
            key,
            label,
            area,
            count,
            latestNumber,
            latestId,
            latestDate,
            ready ? "Ready" : "Missing sample",
            endpoint,
            ready
                ? $"Latest sample {latestNumber} is available for print verification."
                : $"No sample {label} record is available yet."));
    }
}
