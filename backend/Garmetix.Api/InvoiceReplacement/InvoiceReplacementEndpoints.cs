using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Inventory;
using Garmetix.Api.Billing;
using Garmetix.Api.Numbering;
using Garmetix.Api.Purchase;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.InvoiceReplacement;

public static class InvoiceReplacementEndpoints
{
    public static RouteGroupBuilder MapInvoiceReplacementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/invoice-replacements")
            .WithTags("Invoice Replacements")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/pending", GetPendingAsync);
        group.MapGet("/audit", GetAuditAsync);
        group.MapPost("/sales/{revisedInvoiceId:guid}/approve", ApproveSalesReplacementAsync);
        group.MapPost("/purchase/{revisedInvoiceId:guid}/approve", ApprovePurchaseReplacementAsync);

        return group;
    }

    private static async Task<IReadOnlyList<InvoiceReplacementPendingDto>> GetPendingAsync(
        HttpContext context,
        GarmetixDbContext db,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 250);
        var rows = new List<InvoiceReplacementPendingDto>();

        var revisedSales = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => item.OriginalInvoiceId.HasValue && !item.ReturnInvoice)
            .OrderByDescending(item => item.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var saleOriginalIds = revisedSales.Select(item => item.OriginalInvoiceId!.Value).Distinct().ToList();
        var saleOriginals = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => saleOriginalIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var saleStoreIds = revisedSales.Select(item => item.StoreId)
            .Concat(saleOriginals.Values.Select(item => item.StoreId))
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList();
        var saleStores = await db.Stores.AsNoTracking()
            .Where(item => saleStoreIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.StoreGroupId, cancellationToken);

        foreach (var revised in revisedSales)
        {
            if (!revised.OriginalInvoiceId.HasValue || !saleOriginals.TryGetValue(revised.OriginalInvoiceId.Value, out var original))
            {
                continue;
            }
            if (original.InvoiceStatus == InvoiceStatus.Cancelled)
            {
                continue;
            }
            var checks = BuildChecks(revised.CompanyId == original.CompanyId, revised.StoreId == original.StoreId, original.InvoiceStatus != InvoiceStatus.Cancelled, revised.InvoiceStatus != InvoiceStatus.Cancelled);
            rows.Add(new InvoiceReplacementPendingDto(
                "Sale",
                revised.Id,
                revised.InvoiceNumber,
                revised.OnDate,
                revised.BillAmount,
                revised.InvoiceStatus.ToString(),
                original.Id,
                original.InvoiceNumber,
                original.OnDate,
                original.BillAmount,
                original.InvoiceStatus.ToString(),
                revised.CompanyId,
                saleStores.TryGetValue(revised.StoreId, out var storeGroupId) ? storeGroupId : null,
                revised.StoreId,
                revised.CustomerName ?? "Customer",
                checks.All(item => item.StartsWith("OK:", StringComparison.OrdinalIgnoreCase)),
                checks));
        }

        var revisedPurchases = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => item.OriginalInvoiceId.HasValue)
            .OrderByDescending(item => item.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var purchaseOriginalIds = revisedPurchases.Select(item => item.OriginalInvoiceId!.Value).Distinct().ToList();
        var purchaseOriginals = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => purchaseOriginalIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var revised in revisedPurchases)
        {
            if (!revised.OriginalInvoiceId.HasValue || !purchaseOriginals.TryGetValue(revised.OriginalInvoiceId.Value, out var original))
            {
                continue;
            }
            if (original.InvoiceStatus == InvoiceStatus.Cancelled)
            {
                continue;
            }
            var checks = BuildChecks(revised.CompanyId == original.CompanyId, revised.StoreId == original.StoreId, original.InvoiceStatus != InvoiceStatus.Cancelled, revised.InvoiceStatus != InvoiceStatus.Cancelled);
            rows.Add(new InvoiceReplacementPendingDto(
                "Purchase",
                revised.Id,
                string.IsNullOrWhiteSpace(revised.InwardNumber) ? revised.InvoiceNumber : revised.InwardNumber,
                revised.InwardDate,
                revised.BillAmount,
                revised.InvoiceStatus.ToString(),
                original.Id,
                string.IsNullOrWhiteSpace(original.InwardNumber) ? original.InvoiceNumber : original.InwardNumber,
                original.InwardDate,
                original.BillAmount,
                original.InvoiceStatus.ToString(),
                revised.CompanyId,
                revised.StoreGroupId,
                revised.StoreId,
                revised.VendorName ?? "Supplier",
                checks.All(item => item.StartsWith("OK:", StringComparison.OrdinalIgnoreCase)),
                checks));
        }

        return rows
            .OrderByDescending(item => item.RevisedDate)
            .Take(limit)
            .ToList();
    }

    private static async Task<IReadOnlyList<InvoiceReplacementAuditDto>> GetAuditAsync(
        GarmetixDbContext db,
        int take = 150,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 500);
        return await db.AuditLogEntries.AsNoTracking()
            .Where(item => !item.Deleted && item.Module == InvoiceReplacementAudit.Module)
            .OrderByDescending(item => item.OccurredAt)
            .Take(limit)
            .Select(item => new InvoiceReplacementAuditDto(
                item.Id,
                item.OccurredAt,
                item.Action,
                item.EntityDisplayName,
                item.EntityId,
                item.Reference,
                item.UserName,
                item.Reason,
                item.RequestPath))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> ApproveSalesReplacementAsync(
        Guid revisedInvoiceId,
        InvoiceReplacementApprovalRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var revised = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == revisedInvoiceId, cancellationToken);
        if (revised is null)
        {
            return Results.NotFound(new { message = "Revised sale invoice was not found." });
        }
        if (!revised.OriginalInvoiceId.HasValue)
        {
            return Results.BadRequest(new { message = "This sale invoice is not linked to an original invoice replacement." });
        }
        var original = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == revised.OriginalInvoiceId.Value, cancellationToken);
        if (original is null)
        {
            return Results.NotFound(new { message = "Original sale invoice was not found." });
        }
        if (original.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Original sale invoice is already cancelled." });
        }
        if (revised.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Revised sale invoice is cancelled and cannot approve replacement." });
        }
        if (revised.CompanyId != original.CompanyId || revised.StoreId != original.StoreId)
        {
            return Results.Conflict(new { message = "Original and revised sale invoices must belong to the same company/store." });
        }

        var storeGroupId = await db.Stores.AsNoTracking()
            .Where(item => item.Id == revised.StoreId)
            .Select(item => (Guid?)item.StoreGroupId)
            .FirstOrDefaultAsync(cancellationToken);
        var note = CleanNote(request.ApprovalNote);
        var reason = $"Replacement approved. Original sale {original.InvoiceNumber} replaced by revised sale {revised.InvoiceNumber}. {note}".Trim();
        InvoiceReplacementAudit.Add(db, context, "Approved", nameof(Invoice), revised.Id, "Sales Invoice Replacement", revised.InvoiceNumber, revised.CompanyId, storeGroupId, revised.StoreId, reason, before: Snapshot(original), after: Snapshot(revised));

        var cancelResult = await BillingEndpoints.CancelSaleForReplacementApprovalAsync(
            original.Id,
            new CancelInvoiceRequest(reason),
            context,
            db,
            accounting,
            stockLedger,
            cancellationToken);

        await db.Entry(original).ReloadAsync(cancellationToken);
        if (original.InvoiceStatus != InvoiceStatus.Cancelled)
        {
            InvoiceReplacementAudit.Add(db, context, "ApprovalFailed", nameof(Invoice), revised.Id, "Sales Invoice Replacement", revised.InvoiceNumber, revised.CompanyId, storeGroupId, revised.StoreId, "Approval failed because cancellation/reversal did not complete.", before: Snapshot(original), after: Snapshot(revised));
            await db.SaveChangesAsync(cancellationToken);
            return cancelResult;
        }

        revised.UpdatedAt = DateTime.UtcNow;
        InvoiceReplacementAudit.Add(db, context, "Completed", nameof(Invoice), revised.Id, "Sales Invoice Replacement", revised.InvoiceNumber, revised.CompanyId, storeGroupId, revised.StoreId, "Old sale invoice cancelled after approval; stock, payment and accounting reversal posted.", before: Snapshot(original), after: Snapshot(revised));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new InvoiceReplacementApprovalResultDto("Sale", revised.Id, revised.InvoiceNumber, original.Id, original.InvoiceNumber, original.InvoiceStatus.ToString(), true, "Replacement approved and original sale invoice reversed."));
    }

    private static async Task<IResult> ApprovePurchaseReplacementAsync(
        Guid revisedInvoiceId,
        InvoiceReplacementApprovalRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService numbering,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var revised = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == revisedInvoiceId, cancellationToken);
        if (revised is null)
        {
            return Results.NotFound(new { message = "Revised purchase invoice was not found." });
        }
        if (!revised.OriginalInvoiceId.HasValue)
        {
            return Results.BadRequest(new { message = "This purchase invoice is not linked to an original invoice replacement." });
        }
        var original = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == revised.OriginalInvoiceId.Value, cancellationToken);
        if (original is null)
        {
            return Results.NotFound(new { message = "Original purchase invoice was not found." });
        }
        if (original.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Original purchase invoice is already cancelled." });
        }
        if (original.InvoiceStatus is InvoiceStatus.PartiallyRefunded or InvoiceStatus.Refunded)
        {
            return Results.Conflict(new { message = "Original purchase invoice already has returns; use purchase-return flow before replacement approval." });
        }
        if (revised.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Revised purchase invoice is cancelled and cannot approve replacement." });
        }
        if (revised.CompanyId != original.CompanyId || revised.StoreId != original.StoreId)
        {
            return Results.Conflict(new { message = "Original and revised purchase invoices must belong to the same company/store." });
        }

        var note = CleanNote(request.ApprovalNote);
        var revisedNumber = string.IsNullOrWhiteSpace(revised.InwardNumber) ? revised.InvoiceNumber : revised.InwardNumber;
        var originalNumber = string.IsNullOrWhiteSpace(original.InwardNumber) ? original.InvoiceNumber : original.InwardNumber;
        var reason = $"Replacement approved. Original purchase {originalNumber} replaced by revised inward {revisedNumber}. {note}".Trim();
        InvoiceReplacementAudit.Add(db, context, "Approved", nameof(PurchaseInvoice), revised.Id, "Purchase Invoice Replacement", revisedNumber, revised.CompanyId, revised.StoreGroupId, revised.StoreId, reason, before: Snapshot(original), after: Snapshot(revised));

        var cancelResult = await PurchaseEndpoints.CancelPurchaseForReplacementApprovalAsync(
            original.Id,
            new CancelPurchaseInvoiceRequest(reason),
            context,
            db,
            accounting,
            numbering,
            stockLedger,
            cancellationToken);

        await db.Entry(original).ReloadAsync(cancellationToken);
        if (original.InvoiceStatus != InvoiceStatus.Cancelled)
        {
            InvoiceReplacementAudit.Add(db, context, "ApprovalFailed", nameof(PurchaseInvoice), revised.Id, "Purchase Invoice Replacement", revisedNumber, revised.CompanyId, revised.StoreGroupId, revised.StoreId, "Approval failed because purchase cancellation/reversal did not complete.", before: Snapshot(original), after: Snapshot(revised));
            await db.SaveChangesAsync(cancellationToken);
            return cancelResult;
        }

        revised.UpdatedAt = DateTime.UtcNow;
        InvoiceReplacementAudit.Add(db, context, "Completed", nameof(PurchaseInvoice), revised.Id, "Purchase Invoice Replacement", revisedNumber, revised.CompanyId, revised.StoreGroupId, revised.StoreId, "Old purchase invoice cancelled after approval; stock, GST ITC, vendor and accounting reversal posted.", before: Snapshot(original), after: Snapshot(revised));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new InvoiceReplacementApprovalResultDto("Purchase", revised.Id, revisedNumber, original.Id, originalNumber, original.InvoiceStatus.ToString(), true, "Replacement approved and original purchase invoice reversed."));
    }

    private static IReadOnlyList<string> BuildChecks(bool sameCompany, bool sameStore, bool originalOpen, bool revisedOpen)
    {
        var checks = new List<string>
        {
            sameCompany ? "OK: same company" : "BLOCK: company mismatch",
            sameStore ? "OK: same store" : "BLOCK: store mismatch",
            originalOpen ? "OK: original invoice can be reversed" : "BLOCK: original invoice is already cancelled",
            revisedOpen ? "OK: revised invoice is active" : "BLOCK: revised invoice is cancelled"
        };
        return checks;
    }

    private static string CleanNote(string? note)
        => string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim();

    private static object Snapshot(Invoice invoice) => new
    {
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.OnDate,
        invoice.InvoiceStatus,
        invoice.BillAmount,
        invoice.PaidAmount,
        invoice.CustomerName,
        invoice.StoreId,
        invoice.CompanyId,
        invoice.OriginalInvoiceId
    };

    private static object Snapshot(PurchaseInvoice invoice) => new
    {
        invoice.Id,
        invoice.InvoiceNumber,
        invoice.InwardNumber,
        invoice.InwardDate,
        invoice.InvoiceStatus,
        invoice.BillAmount,
        invoice.VendorName,
        invoice.StoreGroupId,
        invoice.StoreId,
        invoice.CompanyId,
        invoice.OriginalInvoiceId
    };
}
