using Garmetix.Api.Accounting;
using Garmetix.Api.Auth;
using Garmetix.Api.Commercial;
using Garmetix.Api.Gstin;
using Garmetix.Api.Inventory;
using Garmetix.Api.InvoiceReplacement;
using Garmetix.Api.Numbering;
using Garmetix.Api.ProductLookup;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.HRM;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Purchase;

public static class PurchaseEndpoints
{
    public static RouteGroupBuilder MapPurchaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase")
            .WithTags("Purchase")
            .RequireAuthorization(GarmetixPolicies.Purchase);

        group.MapGet("/lookup-options", GetLookupOptionsAsync);
        group.MapGet("/invoices", SearchPurchaseInvoicesAsync);
        group.MapGet("/invoices/recent", GetRecentPurchaseInvoicesAsync);
        group.MapGet("/payments", SearchPurchasePaymentsAsync);
        group.MapGet("/payments/recent", GetRecentPurchasePaymentsAsync);
        group.MapGet("/payments/{id:guid}", GetPurchasePaymentAsync);
        group.MapPut("/payments/{id:guid}", UpdatePurchasePaymentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/payments/{id:guid}", DeletePurchasePaymentAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapGet("/payments/reconciliation", GetPurchasePaymentReconciliationAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapPost("/payments/reconciliation/repair", RepairPurchasePaymentReconciliationAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/returns/recent", GetRecentPurchaseReturnsAsync);
        group.MapGet("/returns/{id:guid}", GetPurchaseReturnAsync);
        group.MapGet("/returns/{id:guid}/reconciliation", GetPurchaseReturnReconciliationAsync);
        group.MapGet("/returns/{id:guid}/pdf", DownloadPurchaseReturnPdfAsync);
        group.MapPost("/returns/{id:guid}/mark-printed", MarkPurchaseReturnPrintedAsync);
        group.MapPost("/returns/{id:guid}/reverse", ReversePurchaseReturnAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapGet("/invoices/{id:guid}/receipt", GetReceiptAsync);
        group.MapGet("/invoices/{id:guid}/returnable", GetReturnablePurchaseInvoiceAsync);
        group.MapGet("/invoices/{id:guid}/pdf", DownloadPurchasePdfAsync);
        group.MapPut("/invoices/{id:guid}", UpdatePurchaseInvoiceAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapDelete("/invoices/{id:guid}", DeletePurchaseInvoiceAsync).RequireAuthorization(GarmetixPolicies.Delete);
        group.MapPost("/inward", CreateInwardAsync);
        group.MapPost("/invoices/{id:guid}/partial-return", CreatePartialPurchaseReturnAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapGet("/vendors/{vendorId:guid}/returnable-items", SearchVendorReturnableItemsAsync);
        group.MapPost("/vendors/{vendorId:guid}/goods-return", CreateVendorGoodsReturnAsync).RequireAuthorization(GarmetixPolicies.Edit);
        group.MapPost("/invoices/{id:guid}/payment-voucher", CreateVendorPaymentVoucherAsync);
        group.MapPost("/payments/advance", CreateVendorAdvancePaymentAsync);
        group.MapPost("/payments/{id:guid}/send-email", SendVendorPaymentEmailAsync);
        group.MapPost("/invoices/{id:guid}/cancel", CancelPurchaseAsync).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }

    private static async Task<PurchaseLookupOptionsDto> GetLookupOptionsAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var categories = await WorkspaceScope.ApplyTo(db.ProductCategories.AsNoTracking(), context)
            .Where(item => item.IsActive)
            .OrderBy(item => item.ProductGroup)
            .ThenBy(item => item.Name)
            .Select(item => new PurchaseLookupOptionDto(item.Id, item.Name))
            .ToListAsync(cancellationToken);

        var subCategories = await WorkspaceScope.ApplyTo(db.ProductSubCategories.AsNoTracking(), context)
            .OrderBy(item => item.Name)
            .Select(item => new PurchaseSubCategoryOptionDto(item.Id, item.Name, item.CategoryId))
            .ToListAsync(cancellationToken);

        var taxes = await WorkspaceScope.ApplyTo(db.Taxes.AsNoTracking(), context)
            .OrderBy(item => item.TaxType)
            .ThenBy(item => item.CompositeRate)
            .Select(item => new PurchaseTaxOptionDto(item.Id, string.IsNullOrWhiteSpace(item.Name) ? $"GST {item.CompositeRate:N2}%" : item.Name, item.CompositeRate, item.TaxType.ToString()))
            .ToListAsync(cancellationToken);

        var vendors = await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
            .Where(item => item.Active)
            .OrderBy(item => item.Name)
            .Select(item => new PurchaseVendorOptionDto(
                item.Id,
                item.Name,
                item.MobileNumber,
                item.GSTIN,
                item.BillAmount,
                item.Paid,
                item.BillAmount - item.Paid))
            .ToListAsync(cancellationToken);

        return new PurchaseLookupOptionsDto(
            categories,
            subCategories,
            taxes,
            vendors,
            EnumOptions<Unit>(),
            EnumOptions<ProductType>(),
            EnumOptions<ProductGroup>());
    }

    private static async Task<PagedPurchaseInvoicesDto> SearchPurchaseInvoicesAsync(
        HttpContext context,
        GarmetixDbContext db,
        int? year,
        int? month,
        int page = 1,
        int pageSize = 50,
        string? q = null,
        string? status = null,
        string? dateMode = "inward",
        Guid? vendorId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var useEntryDate = string.Equals(dateMode, "entry", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dateMode, "onDate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(dateMode, "created", StringComparison.OrdinalIgnoreCase);
        var now = DateTime.Today;
        var selectedYear = year.GetValueOrDefault(now.Year);
        var selectedMonth = month.GetValueOrDefault(now.Month);
        if (selectedMonth < 1 || selectedMonth > 12)
        {
            selectedMonth = now.Month;
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 25, 200);

        var fromDate = from?.Date ?? new DateTime(selectedYear, selectedMonth, 1);
        var toDateExclusive = to?.Date.AddDays(1) ?? fromDate.AddMonths(1);
        var term = q?.Trim().ToLowerInvariant();

        var query = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context);
        query = useEntryDate
            ? query.Where(item => item.OnDate >= fromDate && item.OnDate < toDateExclusive)
            : query.Where(item => item.InwardDate >= fromDate && item.InwardDate < toDateExclusive);

        if (vendorId.HasValue)
        {
            query = query.Where(item => item.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<InvoiceStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(item => item.InvoiceStatus == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                item.InvoiceNumber.ToLower().Contains(term) ||
                item.InwardNumber.ToLower().Contains(term) ||
                (item.VendorName != null && item.VendorName.ToLower().Contains(term)) ||
                (item.VendorGSTIN != null && item.VendorGSTIN.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                BillAmount = group.Sum(item => item.BillAmount),
                FreightAmount = group.Sum(item => item.FrightAmount),
                CancelledCount = group.Count(item => item.InvoiceStatus == InvoiceStatus.Cancelled)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var orderedQuery = useEntryDate
            ? query.OrderByDescending(item => item.OnDate).ThenByDescending(item => item.CreatedAt)
            : query.OrderByDescending(item => item.InwardDate).ThenByDescending(item => item.CreatedAt);

        var invoices = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).ToArray();
        var paidLookup = await GetPaidAmountLookupAsync(invoiceIds, db, cancellationToken);
        var importProofLookup = await GetPurchaseImportProofLookupAsync(invoiceIds, context, db, cancellationToken);
        var items = invoices.Select(invoice =>
        {
            paidLookup.TryGetValue(invoice.Id, out var paidAmount);
            importProofLookup.TryGetValue(invoice.Id, out var importBatchId);
            paidAmount = invoice.InvoiceStatus == InvoiceStatus.Cancelled ? 0 : paidAmount;
            return new RecentPurchaseInvoiceDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InwardNumber,
                invoice.OnDate,
                invoice.InwardDate,
                invoice.SupplierInvoiceDate,
                invoice.DueDate,
                invoice.VendorId,
                invoice.VendorName ?? "Supplier",
                invoice.VendorGSTIN,
                invoice.BillAmount,
                paidAmount,
                Math.Max(invoice.BillAmount - paidAmount, 0),
                invoice.FrightAmount,
                invoice.ItemCount,
                invoice.Quantity,
                invoice.InvoiceStatus.ToString(),
                invoice.PaymentMode?.ToString() ?? "-",
                importBatchId != Guid.Empty,
                importBatchId == Guid.Empty ? null : importBatchId
            );
        }).ToList();

        return new PagedPurchaseInvoicesDto(
            items,
            total,
            page,
            pageSize,
            selectedYear,
            selectedMonth,
            totals?.BillAmount ?? 0,
            items.Sum(item => item.PaidAmount),
            totals?.FreightAmount ?? 0,
            totals?.CancelledCount ?? 0);
    }

    private static async Task<IReadOnlyList<RecentPurchaseInvoiceDto>> GetRecentPurchaseInvoicesAsync(HttpContext context, GarmetixDbContext db, int take = 50, CancellationToken cancellationToken = default)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).ToArray();
        var paidLookup = await GetPaidAmountLookupAsync(invoiceIds, db, cancellationToken);
        var importProofLookup = await GetPurchaseImportProofLookupAsync(invoiceIds, context, db, cancellationToken);

        return invoices.Select(invoice =>
        {
            paidLookup.TryGetValue(invoice.Id, out var paidAmount);
            importProofLookup.TryGetValue(invoice.Id, out var importBatchId);
            paidAmount = invoice.InvoiceStatus == InvoiceStatus.Cancelled ? 0 : paidAmount;
            return new RecentPurchaseInvoiceDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.InwardNumber,
                invoice.OnDate,
                invoice.InwardDate,
                invoice.SupplierInvoiceDate,
                invoice.DueDate,
                invoice.VendorId,
                invoice.VendorName ?? "Supplier",
                invoice.VendorGSTIN,
                invoice.BillAmount,
                paidAmount,
                Math.Max(invoice.BillAmount - paidAmount, 0),
                invoice.FrightAmount,
                invoice.ItemCount,
                invoice.Quantity,
                invoice.InvoiceStatus.ToString(),
                invoice.PaymentMode?.ToString() ?? "-",
                importBatchId != Guid.Empty,
                importBatchId == Guid.Empty ? null : importBatchId
            );
        }).ToList();
    }

    private static async Task<IReadOnlyList<PurchaseReturnRegisterDto>> GetRecentPurchaseReturnsAsync(
        HttpContext context,
        GarmetixDbContext db,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        return await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 250))
            .Select(item => new PurchaseReturnRegisterDto(
                item.Id,
                item.ReturnNumber,
                item.OnDate,
                item.ReturnKind,
                item.Status,
                item.PurchaseInvoiceId,
                item.OriginalInvoiceNumber,
                item.VendorId,
                item.VendorName,
                item.VendorGstin,
                item.Quantity,
                item.TaxableAmount,
                item.TaxAmount,
                item.ReturnAmount,
                item.ItemCount,
                item.DebitNoteId,
                item.DebitNoteNumber,
                item.Reason,
                item.Printed,
                item.PrintCount,
                item.LastPrintedAt,
                item.PrintCount > 1 ? "Reprinted" : item.Printed ? "Printed" : "Not Printed",
                item.SettledAmount,
                Math.Max(item.ReturnAmount - item.SettledAmount, 0),
                item.SettlementStatus,
                item.ItcReversalAmount,
                item.ItcReversalStatus,
                item.JournalEntryId))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IResult> GetPurchaseReturnAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await LoadPurchaseReturnAsync(id, context, db, cancellationToken);
        return item is null
            ? Results.NotFound(new { message = "Purchase return was not found." })
            : Results.Ok(item);
    }

    private static async Task<IResult> DownloadPurchaseReturnPdfAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        string? format,
        string? copy,
        bool reprint = false,
        bool signatures = true,
        CancellationToken cancellationToken = default)
    {
        var purchaseReturn = await LoadPurchaseReturnAsync(id, context, db, cancellationToken);
        if (purchaseReturn is null)
        {
            return Results.NotFound(new { message = "Purchase return was not found." });
        }

        var scope = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .Where(item => item.Id == id)
            .Select(item => new { item.CompanyId, item.StoreId })
            .FirstAsync(cancellationToken);
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == scope.CompanyId, cancellationToken);
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == scope.StoreId, cancellationToken);

        var pdf = PurchaseReturnPdfDocument.Build(
            new PurchaseReturnPdfModel(
                purchaseReturn,
                company?.Name ?? "Garmetix",
                BuildCompanyAddress(company),
                company?.ContactNumber ?? string.Empty,
                company?.GSTIN ?? string.Empty,
                store?.Name ?? "Store",
                DocumentCodeService.Create(DocumentCodeService.PurchaseReturn, purchaseReturn.Id)),
            format,
            copy,
            reprint,
            signatures);

        var safeNumber = new string(purchaseReturn.ReturnNumber.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "purchase-return")}-{NormalizePdfFormat(format)}.pdf");
    }

    private static async Task<IResult> MarkPurchaseReturnPrintedAsync(
        Guid id,
        PurchaseReturnPrintRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var purchaseReturn = await WorkspaceScope.ApplyTo(db.PurchaseReturns, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (purchaseReturn is null)
        {
            return Results.NotFound(new { message = "Purchase return was not found." });
        }

        purchaseReturn.Printed = true;
        purchaseReturn.PrintCount = Math.Max(purchaseReturn.PrintCount, 0) + 1;
        purchaseReturn.LastPrintedAt = DateTime.Now;
        purchaseReturn.UpdatedAt = DateTime.Now;
        if (purchaseReturn.DebitNoteId.HasValue)
        {
            var note = await db.CommercialNotes.FirstOrDefaultAsync(item => item.Id == purchaseReturn.DebitNoteId.Value, cancellationToken);
            if (note is not null)
            {
                note.Printed = true;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new PurchaseReturnPrintResponse(
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            purchaseReturn.Printed,
            purchaseReturn.PrintCount,
            purchaseReturn.LastPrintedAt.Value,
            PurchaseReturnPrintStatus(purchaseReturn.Printed, purchaseReturn.PrintCount)));
    }

    private static async Task<PurchaseReturnDetailDto?> LoadPurchaseReturnAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .FirstOrDefaultAsync(row => row.Id == id && !row.Deleted, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var returnItemRows = await db.PurchaseReturnItems.AsNoTracking()
            .Where(row => row.PurchaseReturnId == item.Id)
            .OrderBy(row => row.ProductName)
            .ToListAsync(cancellationToken);

        var sourceInvoiceIds = returnItemRows.Select(row => row.PurchaseInvoiceId).Distinct().ToArray();
        var rowNumberLookup = new Dictionary<Guid, int>();
        foreach (var sourceInvoiceId in sourceInvoiceIds)
        {
            var originalRowNumbers = await db.PurchaseInvoiceItems.AsNoTracking()
                .Where(row => row.InvoiceId == sourceInvoiceId)
                .OrderBy(row => row.CreatedAt)
                .ThenBy(row => row.Id)
                .Select(row => row.Id)
                .ToListAsync(cancellationToken);
            var rowNumber = 0;
            foreach (var invoiceItemId in originalRowNumbers)
            {
                rowNumber++;
                rowNumberLookup[invoiceItemId] = rowNumber;
            }
        }

        var originalInvoiceNumbers = returnItemRows.Count == 0
            ? new List<string> { item.OriginalInvoiceNumber }
            : await db.PurchaseInvoices.AsNoTracking()
                .Where(row => sourceInvoiceIds.Contains(row.Id))
                .OrderBy(row => row.OnDate)
                .Select(row => row.InvoiceNumber)
                .ToListAsync(cancellationToken);
        if (originalInvoiceNumbers.Count == 0)
        {
            originalInvoiceNumbers.Add(item.OriginalInvoiceNumber);
        }

        var items = returnItemRows.Select(row => new PurchaseReturnItemDto(
            row.Id,
            row.PurchaseInvoiceItemId,
            row.ProductId,
            row.ProductName,
            row.Barcode,
            row.HSNCode,
            row.Unit.HasValue ? row.Unit.Value.ToString() : Unit.Pcs.ToString(),
            row.PurchasedQuantity,
            row.PreviouslyReturnedQuantity,
            row.ReturnedQuantity,
            row.MRP,
            row.UnitRate,
            row.DiscountAmount,
            row.TaxableAmount,
            row.TaxRate,
            row.TaxAmount,
            row.CGSTAmount,
            row.SGSTAmount,
            row.IGSTAmount,
            row.ReturnAmount,
            row.Reason,
            rowNumberLookup.GetValueOrDefault(row.PurchaseInvoiceItemId, 0)))
            .ToList();

        return new PurchaseReturnDetailDto(
            item.Id,
            item.ReturnNumber,
            item.OnDate,
            item.ReturnKind,
            item.Status,
            item.PurchaseInvoiceId,
            item.OriginalInvoiceNumber,
            item.OriginalInvoiceDate,
            item.SupplierInvoiceDate,
            item.VendorId,
            item.VendorName,
            item.VendorGstin,
            item.Quantity,
            item.TaxableAmount,
            item.TaxAmount,
            item.CGSTAmount,
            item.SGSTAmount,
            item.IGSTAmount,
            item.ReturnAmount,
            item.DebitNoteId,
            item.DebitNoteNumber,
            item.Reason,
            item.Printed,
            item.PrintCount,
            item.LastPrintedAt,
            PurchaseReturnPrintStatus(item.Printed, item.PrintCount),
            item.SettledAmount,
            Math.Max(item.ReturnAmount - item.SettledAmount, 0),
            item.SettlementStatus,
            item.ItcReversalAmount,
            item.ItcReversalStatus,
            item.JournalEntryId,
            items,
            item.TransportDetails,
            item.FreightAmount,
            item.FreightBearer,
            item.FreightExpenseVoucherNumber,
            Math.Max(0, (item.OnDate.Date - item.OriginalInvoiceDate.Date).Days),
            originalInvoiceNumbers,
            item.FreightTaxAmount);
    }

    private static async Task<IResult> GetPurchaseReturnReconciliationAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var purchaseReturn = await WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (purchaseReturn is null)
        {
            return Results.NotFound(new { message = "Purchase return was not found." });
        }

        var items = await db.PurchaseReturnItems.AsNoTracking()
            .Where(item => item.PurchaseReturnId == id)
            .ToListAsync(cancellationToken);
        var reversalRows = await db.PurchaseReturnItcReversals.AsNoTracking()
            .Where(item => item.PurchaseReturnId == id)
            .OrderBy(item => item.ProductName)
            .ToListAsync(cancellationToken);
        var stockQuantityOut = await db.StockMovements.AsNoTracking()
            .Where(item => item.SourceType == "PurchaseReturn" && item.SourceId == id)
            .SumAsync(item => item.QuantityOut, cancellationToken);
        var debitNote = purchaseReturn.DebitNoteId.HasValue
            ? await db.CommercialNotes.AsNoTracking().FirstOrDefaultAsync(item => item.Id == purchaseReturn.DebitNoteId.Value, cancellationToken)
            : null;
        var journal = purchaseReturn.JournalEntryId.HasValue
            ? await db.JournalEntries.AsNoTracking().FirstOrDefaultAsync(item => item.Id == purchaseReturn.JournalEntryId.Value, cancellationToken)
            : await db.JournalEntries.AsNoTracking().FirstOrDefaultAsync(item =>
                (item.SourceType == "PurchaseReturn" && item.SourceId == id) ||
                (purchaseReturn.ReturnKind == "Cancellation" &&
                 item.SourceType == "PurchaseInvoiceCancellation" &&
                 item.SourceId == purchaseReturn.PurchaseInvoiceId), cancellationToken);

        var journalLines = journal is null
            ? []
            : await db.JournalLines.AsNoTracking()
                .Where(item => item.JournalEntryId == journal.Id)
                .ToListAsync(cancellationToken);
        var inputGstLedgerIds = await db.Ledgers.AsNoTracking()
            .Where(item => item.CompanyId == purchaseReturn.CompanyId && item.Name == "Input GST")
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var itemTax = RoundMoney(items.Sum(item => item.TaxAmount));
        var reversalTax = RoundMoney(reversalRows.Sum(item => item.TaxAmount));
        var journalItcCredit = RoundMoney(journalLines
            .Where(item => inputGstLedgerIds.Contains(item.LedgerId))
            .Sum(item => item.Credit));
        var journalDebit = RoundMoney(journalLines.Sum(item => item.Debit));
        var journalCredit = RoundMoney(journalLines.Sum(item => item.Credit));
        var componentTax = RoundMoney(reversalRows.Sum(item => item.CGSTAmount + item.SGSTAmount + item.IGSTAmount));
        var itemQuantity = RoundMoney(items.Sum(item => item.ReturnedQuantity));

        var checks = new List<PurchaseReturnReconciliationCheckDto>
        {
            ReconciliationCheck("items", "Return item count", purchaseReturn.ItemCount == items.Count, purchaseReturn.ItemCount.ToString(), items.Count.ToString(), purchaseReturn.ReturnNumber),
            ReconciliationCheck("quantity", "Returned quantity", MoneyText(purchaseReturn.Quantity), MoneyText(itemQuantity), purchaseReturn.Quantity == itemQuantity, purchaseReturn.ReturnNumber),
            ReconciliationCheck("item-tax", "Item GST equals return GST", MoneyText(purchaseReturn.TaxAmount), MoneyText(itemTax), purchaseReturn.TaxAmount == itemTax, purchaseReturn.ReturnNumber),
            ReconciliationCheck("itc-rows", "Every item has an ITC reversal", items.Count == reversalRows.Count, items.Count.ToString(), reversalRows.Count.ToString(), purchaseReturn.ReturnNumber),
            ReconciliationCheck("itc-total", "ITC reversal equals return GST", MoneyText(purchaseReturn.TaxAmount), MoneyText(reversalTax), purchaseReturn.TaxAmount == reversalTax, purchaseReturn.ReturnNumber),
            ReconciliationCheck("itc-components", "GST components equal ITC reversal", MoneyText(reversalTax), MoneyText(componentTax), reversalTax == componentTax, purchaseReturn.ReturnNumber),
            ReconciliationCheck("stock", "Stock movement equals returned quantity", MoneyText(purchaseReturn.Quantity), MoneyText(stockQuantityOut), purchaseReturn.Quantity == RoundMoney(stockQuantityOut), purchaseReturn.ReturnNumber),
            ReconciliationCheck("debit-note", "Vendor debit note is linked", purchaseReturn.DebitNoteId.HasValue ? purchaseReturn.DebitNoteId.Value.ToString() : "Linked debit note", debitNote?.Id.ToString() ?? "Missing", debitNote is not null, debitNote?.NoteNumber),
            ReconciliationCheck("journal", "Accounting journal is linked", purchaseReturn.JournalEntryId.HasValue ? purchaseReturn.JournalEntryId.Value.ToString() : "Linked journal", journal?.Id.ToString() ?? "Missing", journal is not null, journal?.EntryNumber),
            ReconciliationCheck("journal-balance", "Journal debits equal credits", MoneyText(journalDebit), MoneyText(journalCredit), journal is not null && journalDebit == journalCredit, journal?.EntryNumber),
            ReconciliationCheck("journal-itc", "Input GST credit equals ITC reversal", MoneyText(reversalTax), MoneyText(journalItcCredit), journal is not null && reversalTax == journalItcCredit, journal?.EntryNumber),
            ReconciliationCheck("settlement", "Settlement does not exceed return", $"Up to {MoneyText(purchaseReturn.ReturnAmount)}", MoneyText(purchaseReturn.SettledAmount), purchaseReturn.SettledAmount <= purchaseReturn.ReturnAmount, purchaseReturn.DebitNoteNumber)
        };
        var status = checks.All(item => item.Passed) ? "Reconciled" : "Needs Review";

        return Results.Ok(new PurchaseReturnReconciliationDto(
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            status,
            purchaseReturn.PurchaseInvoiceId,
            purchaseReturn.DebitNoteId,
            purchaseReturn.DebitNoteNumber,
            journal?.Id,
            journal?.EntryNumber,
            purchaseReturn.TaxAmount,
            itemTax,
            reversalTax,
            journalItcCredit,
            RoundMoney(stockQuantityOut),
            purchaseReturn.SettledAmount,
            reversalRows.Select(item => new PurchaseReturnItcReversalDto(
                item.Id,
                item.PurchaseReturnItemId,
                item.PurchaseInvoiceItemId,
                item.ProductName,
                item.HSNCode,
                item.TaxRate,
                item.ReturnedQuantity,
                item.TaxableAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.TaxAmount,
                item.JournalEntryId,
                item.Status)).ToList(),
            checks));
    }

    private static PurchaseReturnReconciliationCheckDto ReconciliationCheck(
        string key,
        string label,
        bool passed,
        string expected,
        string actual,
        string? reference) => new(key, label, passed, expected, actual, reference);

    private static PurchaseReturnReconciliationCheckDto ReconciliationCheck(
        string key,
        string label,
        string expected,
        string actual,
        bool passed,
        string? reference) => new(key, label, passed, expected, actual, reference);

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string MoneyText(decimal value) => RoundMoney(value).ToString("0.00");

    private static async Task<IResult> GetReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var receipt = await LoadReceiptAsync(id, context, db, cancellationToken);
        return receipt is null ? Results.NotFound() : Results.Ok(receipt);
    }

    private static async Task<IResult> DownloadPurchasePdfAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        string? format,
        string? copy,
        bool reprint = false,
        bool signatures = true,
        CancellationToken cancellationToken = default)
    {
        var receipt = await LoadReceiptAsync(id, context, db, cancellationToken);
        if (receipt is null)
        {
            return Results.NotFound();
        }

        var pdf = PurchasePdfDocument.Build(
            new PurchasePdfModel(
                receipt.CompanyName,
                receipt.CompanyAddress,
                receipt.CompanyPhone,
                receipt.CompanyGstin,
                receipt.StoreName,
                receipt.InvoiceNumber,
                receipt.InwardNumber,
                receipt.OnDate,
                receipt.InwardDate,
                receipt.SupplierInvoiceDate,
                receipt.InvoiceStatus,
                receipt.VendorName,
                receipt.VendorGstin,
                receipt.MRP,
                receipt.DiscountAmount,
                receipt.NetAmount,
                receipt.TaxAmount,
                receipt.FreightAmount,
                receipt.RoundOff,
                receipt.BillAmount,
                receipt.PaidAmount,
                receipt.BalanceAmount,
                receipt.Items,
                Garmetix.Api.ProductLookup.DocumentCodeService.Create(Garmetix.Api.ProductLookup.DocumentCodeService.PurchaseInvoice, receipt.Id)),
            format,
            copy,
            reprint,
            signatures);

        var safeNumber = new string(receipt.InvoiceNumber.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_').ToArray());
        return Results.File(pdf, "application/pdf", $"{(safeNumber.Length > 0 ? safeNumber : "purchase-invoice")}-{NormalizePdfFormat(format)}.pdf");
    }


    private static async Task<IResult> GetReturnablePurchaseInvoiceAsync(Guid id, HttpContext context, GarmetixDbContext db, StockLedgerService stockLedger, CancellationToken cancellationToken)
    {
        var dto = await BuildReturnablePurchaseInvoiceAsync(id, context, db, stockLedger, cancellationToken);
        return dto is null ? Results.NotFound(new { message = "Purchase invoice was not found." }) : Results.Ok(dto);
    }

    private static async Task<ReturnablePurchaseInvoiceDto?> BuildReturnablePurchaseInvoiceAsync(Guid id, HttpContext context, GarmetixDbContext db, StockLedgerService stockLedger, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var items = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var returnedLookup = await GetReturnedQuantityLookupAsync(invoice.Id, invoice.CompanyId, db, cancellationToken);

        var paidAmount = invoice.InvoiceStatus == InvoiceStatus.Cancelled
            ? 0
            : await GetPaidAmountAsync(invoice.Id, db, cancellationToken);

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId");

        var stocks = storeGroupId.HasValue && storeId.HasValue
            ? await db.Stocks.AsNoTracking()
                .Where(stock =>
                    stock.CompanyId == invoice.CompanyId &&
                    stock.StoreGroupId == storeGroupId.Value &&
                    stock.StoreId == storeId.Value &&
                    !stock.IsOFB)
                .ToListAsync(cancellationToken)
            : [];

        var dtoItems = new List<ReturnablePurchaseItemDto>();
        var rowNumber = 0;
        foreach (var item in items)
        {
            rowNumber++;
            returnedLookup.TryGetValue(PurchaseReturnKey(item.ProductId, item.Barcode), out var alreadyReturned);
            alreadyReturned = Math.Min(Math.Max(alreadyReturned, 0), item.BilledQuantity);
            var returnable = Math.Max(item.BilledQuantity - alreadyReturned, 0);
            var quantity = item.BilledQuantity <= 0 ? 1 : item.BilledQuantity;

            var stock = stocks.FirstOrDefault(row => row.ProductId == item.ProductId && row.Barcode == item.Barcode);
            var currentStock = stock is null ? 0 : (await stockLedger.GetSnapshotAsync(stock, cancellationToken)).Quantity;

            dtoItems.Add(new ReturnablePurchaseItemDto(
                item.Id,
                item.ProductId,
                item.ProductName ?? item.Barcode,
                item.Barcode,
                item.HSNCode,
                item.Unit?.ToString() ?? Unit.Pcs.ToString(),
                item.BilledQuantity,
                alreadyReturned,
                invoice.InvoiceStatus == InvoiceStatus.Cancelled ? 0 : returnable,
                Math.Round(item.Amount / quantity, 2),
                Math.Round(item.BasePrice / quantity, 2),
                Math.Round(item.TaxAmount / quantity, 2),
                Math.Round(item.DiscountAmount / quantity, 2),
                item.MRP,
                item.TaxPercentage,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                rowNumber,
                currentStock));
        }

        return new ReturnablePurchaseInvoiceDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            invoice.OnDate,
            invoice.InwardDate,
            invoice.VendorId,
            invoice.VendorName ?? "Supplier",
            invoice.VendorGSTIN,
            invoice.BillAmount,
            paidAmount,
            Math.Max(invoice.BillAmount - paidAmount, 0),
            invoice.InvoiceStatus.ToString(),
            dtoItems);
    }

    private static async Task<IResult> CreatePartialPurchaseReturnAsync(
        Guid id,
        PartialPurchaseReturnRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService numbering,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "Select at least one item to return." });
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return Results.BadRequest(new { message = "Return quantity must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cannot create partial return for a cancelled purchase invoice." });
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == invoice.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Purchase vendor was not found." });
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Could not determine purchase store for stock return." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == storeId && store.CompanyId == invoice.CompanyId && store.StoreGroupId == storeGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Purchase store is outside your access scope." });
        }

        var requestedLookup = request.Items
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var requestedItemIds = requestedLookup.Keys.ToArray();
        var invoiceItems = await db.PurchaseInvoiceItems
            .Where(item => item.InvoiceId == invoice.Id && requestedItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        if (invoiceItems.Count != requestedLookup.Count)
        {
            return Results.BadRequest(new { message = "One or more selected return items were not found in this purchase invoice." });
        }

        var returnedLookup = await GetReturnedQuantityLookupAsync(invoice.Id, invoice.CompanyId, db, cancellationToken);
        var returnDate = (request.ReturnDate ?? DateTime.Now).Date;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Partial purchase return" : request.Reason.Trim();

        var freightAmount = Math.Max(request.FreightAmount ?? 0, 0);
        var freightBearer = string.IsNullOrWhiteSpace(request.FreightBearer) ? null : request.FreightBearer.Trim();
        if (freightAmount > 0 && freightBearer is not "Vendor" and not "InHouse")
        {
            return Results.BadRequest(new { message = "Select who bears the freight cost (Vendor or InHouse) before adding a freight amount." });
        }

        Employee? freightEmployee = null;
        if (freightAmount > 0 && freightBearer == "InHouse")
        {
            if (!request.FreightEmployeeId.HasValue)
            {
                return Results.BadRequest(new { message = "Select who is issuing the in-house freight expense." });
            }

            freightEmployee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == request.FreightEmployeeId.Value, cancellationToken);
            if (freightEmployee is null)
            {
                return Results.BadRequest(new { message = "Selected freight issuer employee was not found." });
            }
        }

        var purchaseReturn = new PurchaseReturn
        {
            ReturnNumber = await numbering.NextPurchaseReturnAsync(invoice.CompanyId, storeGroupId, storeId, returnDate, cancellationToken),
            OnDate = returnDate,
            PurchaseInvoiceId = invoice.Id,
            OriginalInvoiceNumber = invoice.InvoiceNumber,
            OriginalInvoiceDate = invoice.OnDate,
            SupplierInvoiceDate = invoice.SupplierInvoiceDate,
            VendorId = vendor.Id,
            VendorName = vendor.Name,
            VendorGstin = vendor.GSTIN,
            ReturnKind = "Partial",
            Status = "Posted",
            Reason = reason,
            TransportDetails = string.IsNullOrWhiteSpace(request.TransportDetails) ? null : request.TransportDetails.Trim(),
            FreightAmount = freightAmount,
            FreightBearer = freightAmount > 0 ? freightBearer : null,
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };
        db.PurchaseReturns.Add(purchaseReturn);
        decimal returnedQuantity = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal returnAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        var movementRemarks = new List<string>();
        var taxPostings = new List<PurchaseReturnTaxPosting>();

        foreach (var item in invoiceItems)
        {
            var requestedQuantity = requestedLookup[item.Id];
            var key = PurchaseReturnKey(item.ProductId, item.Barcode);
            returnedLookup.TryGetValue(key, out var alreadyReturned);
            var returnableQuantity = Math.Max(item.BilledQuantity - alreadyReturned, 0);

            if (requestedQuantity > returnableQuantity)
            {
                return Results.BadRequest(new { message = $"Return quantity for {item.ProductName ?? item.Barcode} exceeds returnable quantity {returnableQuantity:N2}." });
            }

            await DocumentNumberGenerator.LockStockKeyAsync(db, invoice.CompanyId, storeGroupId, storeId, item.ProductId, item.Barcode, cancellationToken);

            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == invoice.CompanyId &&
                stock.StoreGroupId == storeGroupId &&
                stock.StoreId == storeId &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode &&
                !stock.IsOFB,
                cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock row was not found for {item.ProductName ?? item.Barcode}." });
            }

            var stockSnapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            if (stockSnapshot.Quantity < requestedQuantity)
            {
                return Results.BadRequest(new { message = $"Available stock for {item.ProductName ?? item.Barcode} is lower than requested return quantity." });
            }

            var taxResult = PurchaseReturnItcCalculator.Calculate(
                new PurchaseReturnTaxSource(
                    item.BilledQuantity,
                    item.BasePrice,
                    item.TaxAmount,
                    item.CGSTAmount ?? 0,
                    item.SGSTAmount ?? 0,
                    item.IGSTAmount ?? 0,
                    item.DiscountAmount),
                requestedQuantity);
            var lineTaxable = taxResult.TaxableAmount;
            var lineTax = taxResult.TaxAmount;
            var lineAmount = taxResult.ReturnAmount;
            var lineCgst = taxResult.CgstAmount;
            var lineSgst = taxResult.SgstAmount;
            var lineIgst = taxResult.IgstAmount;
            var lineDiscount = taxResult.DiscountAmount;

            var returnItem = new PurchaseReturnItem
            {
                PurchaseReturnId = purchaseReturn.Id,
                PurchaseInvoiceId = invoice.Id,
                PurchaseInvoiceItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? item.Barcode,
                Barcode = item.Barcode,
                HSNCode = item.HSNCode,
                Unit = item.Unit,
                ProductCategoryId = item.ProductCategoryId,
                ProductSubCategoryId = item.ProductSubCategoryId,
                PurchasedQuantity = item.BilledQuantity,
                PreviouslyReturnedQuantity = alreadyReturned,
                ReturnedQuantity = requestedQuantity,
                MRP = item.MRP,
                UnitRate = item.BilledQuantity <= 0 ? 0 : Math.Round(item.BasePrice / item.BilledQuantity, 2),
                DiscountAmount = lineDiscount,
                TaxableAmount = lineTaxable,
                TaxRate = item.TaxPercentage,
                TaxAmount = lineTax,
                CGSTAmount = lineCgst,
                SGSTAmount = lineSgst,
                IGSTAmount = lineIgst,
                ReturnAmount = lineAmount,
                Reason = reason,
                CompanyId = invoice.CompanyId
            };
            db.PurchaseReturnItems.Add(returnItem);

            var reversal = new PurchaseReturnItcReversal
            {
                PurchaseReturnId = purchaseReturn.Id,
                PurchaseReturnItemId = returnItem.Id,
                PurchaseInvoiceId = invoice.Id,
                PurchaseInvoiceItemId = item.Id,
                ReturnNumber = purchaseReturn.ReturnNumber,
                OriginalInvoiceNumber = invoice.InvoiceNumber,
                OnDate = returnDate,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? item.Barcode,
                HSNCode = item.HSNCode,
                TaxRate = item.TaxPercentage,
                ReturnedQuantity = requestedQuantity,
                TaxableAmount = lineTaxable,
                CGSTAmount = lineCgst,
                SGSTAmount = lineSgst,
                IGSTAmount = lineIgst,
                TaxAmount = lineTax,
                Status = "Posted",
                CompanyId = invoice.CompanyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId
            };
            db.PurchaseReturnItcReversals.Add(reversal);
            taxPostings.Add(new PurchaseReturnTaxPosting(reversal.Id, reversal.ProductName, reversal.HSNCode, reversal.TaxAmount));

            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "PurchaseReturnOut",
                QuantityOut = requestedQuantity,
                CostPrice = stockSnapshot.AverageCost,
                MRP = stock.MRP,
                TaxRate = item.TaxPercentage,
                HSNCode = item.HSNCode ?? stock.HSNCode,
                SourceType = "PurchaseReturn",
                SourceId = purchaseReturn.Id,
                SourceNumber = purchaseReturn.ReturnNumber,
                Remarks = reason,
                OnDate = returnDate,
                CompanyId = invoice.CompanyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId
            }, cancellationToken);

            returnedQuantity += requestedQuantity;
            taxableAmount += lineTaxable;
            taxAmount += lineTax;
            returnAmount += lineAmount;
            cgstAmount += lineCgst;
            sgstAmount += lineSgst;
            igstAmount += lineIgst;
            movementRemarks.Add($"{item.ProductName ?? item.Barcode} x {requestedQuantity:N2}");
            returnedLookup[key] = alreadyReturned + requestedQuantity;
        }

        returnAmount = Math.Round(taxableAmount + taxAmount, 2);

        if (returnedQuantity <= 0 || returnAmount <= 0)
        {
            return Results.BadRequest(new { message = "Selected return has no billable value." });
        }

        var debitNote = await CommercialEndpoints.CreateDebitNoteFromPurchaseReturnAsync(
            invoice,
            vendor,
            reason,
            storeGroupId,
            storeId,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            string.Join(", ", movementRemarks),
            db,
            cancellationToken);

        debitNote.Remarks = $"Partial purchase return: {string.Join(", ", movementRemarks)}";
        debitNote.SourceId = purchaseReturn.Id;
        debitNote.SourceNumber = purchaseReturn.ReturnNumber;
        purchaseReturn.Quantity = returnedQuantity;
        purchaseReturn.TaxableAmount = Math.Round(taxableAmount, 2);
        purchaseReturn.TaxAmount = Math.Round(taxAmount, 2);
        purchaseReturn.CGSTAmount = Math.Round(cgstAmount, 2);
        purchaseReturn.SGSTAmount = Math.Round(sgstAmount, 2);
        purchaseReturn.IGSTAmount = Math.Round(igstAmount, 2);
        purchaseReturn.ReturnAmount = Math.Round(returnAmount, 2);
        purchaseReturn.DebitNoteId = debitNote.Id;
        purchaseReturn.DebitNoteNumber = debitNote.NoteNumber;
        purchaseReturn.ItemCount = invoiceItems.Count;
        purchaseReturn.ItcReversalAmount = Math.Round(taxPostings.Sum(item => item.TaxAmount), 2);
        purchaseReturn.ItcReversalStatus = purchaseReturn.ItcReversalAmount == purchaseReturn.TaxAmount ? "Reconciled" : "Mismatch";

        var vendorFreightAmount = purchaseReturn.FreightBearer == "Vendor" ? purchaseReturn.FreightAmount : 0;
        if (vendorFreightAmount > 0)
        {
            debitNote.TaxableAmount = Math.Round(debitNote.TaxableAmount + vendorFreightAmount, 2);
            debitNote.Amount = Math.Round(debitNote.Amount + vendorFreightAmount, 2);
            debitNote.Remarks = $"{debitNote.Remarks} | Includes freight {vendorFreightAmount:N2} billed to vendor";
        }

        vendor.BillAmount = Math.Max(vendor.BillAmount - Math.Round(returnAmount + vendorFreightAmount, 2), 0);

        var allItems = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .Select(item => new { item.ProductId, item.Barcode, item.BilledQuantity })
            .ToListAsync(cancellationToken);
        var fullyReturned = allItems.All(item =>
        {
            returnedLookup.TryGetValue(PurchaseReturnKey(item.ProductId, item.Barcode), out var quantity);
            return quantity >= item.BilledQuantity;
        });

        invoice.InvoiceStatus = fullyReturned ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded;

        await accounting.PostPurchaseReturnAsync(
            purchaseReturn,
            invoice,
            vendor,
            debitNote.NoteNumber,
            storeGroupId,
            storeId,
            Math.Round(taxableAmount, 2),
            Math.Round(returnAmount, 2),
            taxPostings,
            reason,
            cancellationToken,
            vendorFreightAmount);

        if (purchaseReturn.FreightBearer == "InHouse" && purchaseReturn.FreightAmount > 0 && freightEmployee is not null)
        {
            var freightLedger = await db.Ledgers.FirstOrDefaultAsync(
                ledger => ledger.CompanyId == invoice.CompanyId && ledger.Name == "Transport & Freight Charges",
                cancellationToken);
            if (freightLedger is null)
            {
                return Results.BadRequest(new { message = "Transport & Freight Charges ledger was not found for this company." });
            }

            var voucherResult = await accounting.SaveVoucherInCurrentTransactionAsync(
                new VoucherSaveRequest(
                    null,
                    string.Empty,
                    returnDate,
                    VoucherType.Expense,
                    vendor.Name,
                    $"Freight for purchase return {purchaseReturn.ReturnNumber}",
                    purchaseReturn.FreightAmount,
                    $"Freight paid in-house against purchase return {purchaseReturn.ReturnNumber} ({invoice.InvoiceNumber}).",
                    null,
                    PaymentMode.Cash,
                    null,
                    false,
                    null,
                    freightLedger.Id,
                    freightEmployee.Id,
                    null,
                    invoice.CompanyId,
                    storeGroupId,
                    storeId),
                cancellationToken);

            var freightVoucher = await db.Vouchers.FirstOrDefaultAsync(voucher => voucher.Id == voucherResult.VoucherId, cancellationToken);
            purchaseReturn.FreightExpenseVoucherId = voucherResult.VoucherId;
            purchaseReturn.FreightExpenseVoucherNumber = freightVoucher?.VoucherNumber;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new PartialPurchaseReturnResponse(
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            invoice.Id,
            invoice.InvoiceNumber,
            debitNote.Id,
            debitNote.NoteNumber,
            returnedQuantity,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            invoice.InvoiceStatus.ToString(),
            purchaseReturn.FreightAmount,
            purchaseReturn.FreightBearer,
            purchaseReturn.FreightExpenseVoucherNumber));
    }

    private static async Task<(Guid StoreGroupId, Guid StoreId)?> ResolvePurchaseInvoiceStoreAsync(
        PurchaseInvoice invoice,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (invoice.StoreGroupId.HasValue && invoice.StoreId.HasValue)
        {
            return (invoice.StoreGroupId.Value, invoice.StoreId.Value);
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId");
        return storeGroupId.HasValue && storeId.HasValue ? (storeGroupId.Value, storeId.Value) : null;
    }

    private static async Task<IResult> SearchVendorReturnableItemsAsync(
        Guid vendorId,
        string? query,
        HttpContext context,
        GarmetixDbContext db,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(item => item.Id == vendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.NotFound(new { message = "Vendor was not found." });
        }

        var searchTerm = (query ?? string.Empty).Trim();
        if (searchTerm.Length == 0)
        {
            return Results.Ok(Array.Empty<VendorReturnableItemDto>());
        }

        var storeGroupId = WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var storeId = WorkspaceScope.ClaimGuid(context, "storeId");

        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => item.VendorId == vendorId && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .OrderBy(item => item.OnDate)
            .ToListAsync(cancellationToken);
        if (invoices.Count == 0)
        {
            return Results.Ok(Array.Empty<VendorReturnableItemDto>());
        }

        var scopedInvoices = new List<PurchaseInvoice>();
        foreach (var invoice in invoices)
        {
            if (!storeGroupId.HasValue || !storeId.HasValue)
            {
                scopedInvoices.Add(invoice);
                continue;
            }

            var resolvedStore = await ResolvePurchaseInvoiceStoreAsync(invoice, context, db, cancellationToken);
            if (resolvedStore is not null && resolvedStore.Value.StoreGroupId == storeGroupId.Value && resolvedStore.Value.StoreId == storeId.Value)
            {
                scopedInvoices.Add(invoice);
            }
        }

        if (scopedInvoices.Count == 0)
        {
            return Results.Ok(Array.Empty<VendorReturnableItemDto>());
        }

        var invoiceIds = scopedInvoices.Select(item => item.Id).ToArray();
        var invoiceLookup = scopedInvoices.ToDictionary(item => item.Id);

        var allItems = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .ToListAsync(cancellationToken);

        var rowNumberLookup = new Dictionary<Guid, int>();
        foreach (var group in allItems.GroupBy(item => item.InvoiceId))
        {
            var rowNumber = 0;
            foreach (var row in group.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id))
            {
                rowNumber++;
                rowNumberLookup[row.Id] = rowNumber;
            }
        }

        var matches = allItems.Where(item =>
                string.Equals(item.Barcode, searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (item.ProductName ?? string.Empty).Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => invoiceLookup[item.InvoiceId].OnDate)
            .ThenBy(item => item.CreatedAt)
            .Take(50)
            .ToList();

        if (matches.Count == 0)
        {
            return Results.Ok(Array.Empty<VendorReturnableItemDto>());
        }

        var returnedLookup = await GetReturnedQuantityLookupForInvoicesAsync(invoiceIds, vendor.CompanyId, db, cancellationToken);

        var stockCache = new Dictionary<(Guid StoreGroupId, Guid StoreId), List<Stock>>();
        var results = new List<VendorReturnableItemDto>();
        foreach (var item in matches)
        {
            var invoice = invoiceLookup[item.InvoiceId];
            returnedLookup.TryGetValue(VendorReturnQuantityKey(item.InvoiceId, item.ProductId, item.Barcode), out var alreadyReturned);
            alreadyReturned = Math.Min(Math.Max(alreadyReturned, 0), item.BilledQuantity);
            var returnable = Math.Max(item.BilledQuantity - alreadyReturned, 0);
            if (returnable <= 0)
            {
                continue;
            }

            var resolvedStore = await ResolvePurchaseInvoiceStoreAsync(invoice, context, db, cancellationToken);
            decimal currentStock = 0;
            if (resolvedStore is not null)
            {
                var cacheKey = (resolvedStore.Value.StoreGroupId, resolvedStore.Value.StoreId);
                if (!stockCache.TryGetValue(cacheKey, out var stocks))
                {
                    stocks = await db.Stocks.AsNoTracking()
                        .Where(stock =>
                            stock.CompanyId == vendor.CompanyId &&
                            stock.StoreGroupId == resolvedStore.Value.StoreGroupId &&
                            stock.StoreId == resolvedStore.Value.StoreId &&
                            !stock.IsOFB)
                        .ToListAsync(cancellationToken);
                    stockCache[cacheKey] = stocks;
                }

                var stock = stocks.FirstOrDefault(row => row.ProductId == item.ProductId && row.Barcode == item.Barcode);
                currentStock = stock is null ? 0 : (await stockLedger.GetSnapshotAsync(stock, cancellationToken)).Quantity;
            }

            var quantity = item.BilledQuantity <= 0 ? 1 : item.BilledQuantity;
            results.Add(new VendorReturnableItemDto(
                item.Id,
                item.InvoiceId,
                invoice.InvoiceNumber,
                invoice.OnDate,
                rowNumberLookup.GetValueOrDefault(item.Id, 0),
                item.ProductId,
                item.ProductName ?? item.Barcode,
                item.Barcode,
                item.HSNCode,
                item.Unit?.ToString() ?? Unit.Pcs.ToString(),
                item.BilledQuantity,
                alreadyReturned,
                returnable,
                Math.Round(item.Amount / quantity, 2),
                Math.Round(item.BasePrice / quantity, 2),
                Math.Round(item.TaxAmount / quantity, 2),
                Math.Round(item.DiscountAmount / quantity, 2),
                item.MRP,
                item.TaxPercentage,
                currentStock));
        }

        return Results.Ok(results);
    }

    private static async Task<IResult> CreateVendorGoodsReturnAsync(
        Guid vendorId,
        VendorGoodsReturnRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService numbering,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "Select at least one item to return." });
        }

        if (request.Items.Any(item => item.Quantity <= 0))
        {
            return Results.BadRequest(new { message = "Return quantity must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == vendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.NotFound(new { message = "Vendor was not found." });
        }

        var storeGroupId = WorkspaceScope.ClaimGuid(context, "storeGroupId");
        var storeId = WorkspaceScope.ClaimGuid(context, "storeId");
        if (!storeGroupId.HasValue || !storeId.HasValue)
        {
            return Results.BadRequest(new { message = "Could not determine your current store for this return." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == storeId.Value && store.CompanyId == vendor.CompanyId && store.StoreGroupId == storeGroupId.Value, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Your current store is outside your access scope." });
        }

        var requestedLookup = request.Items
            .GroupBy(item => item.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var requestedItemIds = requestedLookup.Keys.ToArray();

        var invoiceItems = await db.PurchaseInvoiceItems
            .Where(item => requestedItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (invoiceItems.Count != requestedLookup.Count)
        {
            return Results.BadRequest(new { message = "One or more selected return items were not found." });
        }

        var sourceInvoiceIds = invoiceItems.Select(item => item.InvoiceId).Distinct().ToArray();
        var sourceInvoices = await db.PurchaseInvoices
            .Where(item => sourceInvoiceIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        if (sourceInvoices.Count != sourceInvoiceIds.Length)
        {
            return Results.BadRequest(new { message = "One or more original purchase invoices were not found." });
        }

        if (sourceInvoices.Any(item => item.VendorId != vendorId))
        {
            return Results.BadRequest(new { message = "One or more selected items do not belong to this vendor." });
        }

        if (sourceInvoices.Any(item => item.InvoiceStatus == InvoiceStatus.Cancelled))
        {
            return Results.BadRequest(new { message = "Cannot return items from a cancelled purchase invoice." });
        }

        foreach (var sourceInvoice in sourceInvoices)
        {
            var resolvedStore = await ResolvePurchaseInvoiceStoreAsync(sourceInvoice, context, db, cancellationToken);
            if (resolvedStore is null || resolvedStore.Value.StoreGroupId != storeGroupId.Value || resolvedStore.Value.StoreId != storeId.Value)
            {
                return Results.BadRequest(new { message = $"Purchase invoice {sourceInvoice.InvoiceNumber} belongs to a different store and cannot be included in this return." });
            }
        }

        var invoiceLookup = sourceInvoices.ToDictionary(item => item.Id);
        var primaryInvoice = sourceInvoices.OrderBy(item => item.OnDate).ThenBy(item => item.CreatedAt).First();
        var distinctInvoiceNumbers = sourceInvoices.OrderBy(item => item.OnDate).Select(item => item.InvoiceNumber).ToList();
        var earliestInvoiceDate = sourceInvoices.Min(item => item.OnDate);

        var returnedLookup = await GetReturnedQuantityLookupForInvoicesAsync(sourceInvoiceIds, vendor.CompanyId, db, cancellationToken);
        var returnDate = (request.ReturnDate ?? DateTime.Now).Date;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Goods return" : request.Reason.Trim();

        var freightAmount = Math.Max(request.FreightAmount ?? 0, 0);
        var freightBearer = string.IsNullOrWhiteSpace(request.FreightBearer) ? null : request.FreightBearer.Trim();
        if (freightAmount > 0 && freightBearer is not "Vendor" and not "InHouse")
        {
            return Results.BadRequest(new { message = "Select who bears the freight cost (Vendor or InHouse) before adding a freight amount." });
        }

        Employee? freightEmployee = null;
        if (freightAmount > 0 && freightBearer == "InHouse")
        {
            if (!request.FreightEmployeeId.HasValue)
            {
                return Results.BadRequest(new { message = "Select who is issuing the in-house freight expense." });
            }

            freightEmployee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == request.FreightEmployeeId.Value, cancellationToken);
            if (freightEmployee is null)
            {
                return Results.BadRequest(new { message = "Selected freight issuer employee was not found." });
            }
        }

        var purchaseReturn = new PurchaseReturn
        {
            ReturnNumber = await numbering.NextPurchaseReturnAsync(vendor.CompanyId, storeGroupId.Value, storeId.Value, returnDate, cancellationToken),
            OnDate = returnDate,
            PurchaseInvoiceId = primaryInvoice.Id,
            OriginalInvoiceNumber = string.Join(", ", distinctInvoiceNumbers),
            OriginalInvoiceDate = earliestInvoiceDate,
            SupplierInvoiceDate = sourceInvoices.Count == 1 ? primaryInvoice.SupplierInvoiceDate : null,
            VendorId = vendor.Id,
            VendorName = vendor.Name,
            VendorGstin = vendor.GSTIN,
            ReturnKind = "GoodsReturn",
            Status = "Posted",
            Reason = reason,
            TransportDetails = string.IsNullOrWhiteSpace(request.TransportDetails) ? null : request.TransportDetails.Trim(),
            FreightAmount = freightAmount,
            FreightBearer = freightAmount > 0 ? freightBearer : null,
            CompanyId = vendor.CompanyId,
            StoreGroupId = storeGroupId.Value,
            StoreId = storeId.Value
        };
        db.PurchaseReturns.Add(purchaseReturn);

        decimal returnedQuantity = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal returnAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        var movementRemarks = new List<string>();
        var taxPostings = new List<PurchaseReturnTaxPosting>();

        foreach (var item in invoiceItems)
        {
            var sourceInvoice = invoiceLookup[item.InvoiceId];
            var requestedQuantity = requestedLookup[item.Id];
            var key = VendorReturnQuantityKey(item.InvoiceId, item.ProductId, item.Barcode);
            returnedLookup.TryGetValue(key, out var alreadyReturned);
            var returnableQuantity = Math.Max(item.BilledQuantity - alreadyReturned, 0);

            if (requestedQuantity > returnableQuantity)
            {
                return Results.BadRequest(new { message = $"Return quantity for {item.ProductName ?? item.Barcode} exceeds returnable quantity {returnableQuantity:N2}." });
            }

            await DocumentNumberGenerator.LockStockKeyAsync(db, vendor.CompanyId, storeGroupId.Value, storeId.Value, item.ProductId, item.Barcode, cancellationToken);

            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == vendor.CompanyId &&
                stock.StoreGroupId == storeGroupId.Value &&
                stock.StoreId == storeId.Value &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode &&
                !stock.IsOFB,
                cancellationToken);

            if (stock is null)
            {
                return Results.BadRequest(new { message = $"Stock row was not found for {item.ProductName ?? item.Barcode}." });
            }

            var stockSnapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
            if (stockSnapshot.Quantity < requestedQuantity)
            {
                return Results.BadRequest(new { message = $"Available stock for {item.ProductName ?? item.Barcode} is lower than requested return quantity." });
            }

            var taxResult = PurchaseReturnItcCalculator.Calculate(
                new PurchaseReturnTaxSource(
                    item.BilledQuantity,
                    item.BasePrice,
                    item.TaxAmount,
                    item.CGSTAmount ?? 0,
                    item.SGSTAmount ?? 0,
                    item.IGSTAmount ?? 0,
                    item.DiscountAmount),
                requestedQuantity);
            var lineTaxable = taxResult.TaxableAmount;
            var lineTax = taxResult.TaxAmount;
            var lineAmount = taxResult.ReturnAmount;
            var lineCgst = taxResult.CgstAmount;
            var lineSgst = taxResult.SgstAmount;
            var lineIgst = taxResult.IgstAmount;
            var lineDiscount = taxResult.DiscountAmount;

            var returnItem = new PurchaseReturnItem
            {
                PurchaseReturnId = purchaseReturn.Id,
                PurchaseInvoiceId = item.InvoiceId,
                PurchaseInvoiceItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? item.Barcode,
                Barcode = item.Barcode,
                HSNCode = item.HSNCode,
                Unit = item.Unit,
                ProductCategoryId = item.ProductCategoryId,
                ProductSubCategoryId = item.ProductSubCategoryId,
                PurchasedQuantity = item.BilledQuantity,
                PreviouslyReturnedQuantity = alreadyReturned,
                ReturnedQuantity = requestedQuantity,
                MRP = item.MRP,
                UnitRate = item.BilledQuantity <= 0 ? 0 : Math.Round(item.BasePrice / item.BilledQuantity, 2),
                DiscountAmount = lineDiscount,
                TaxableAmount = lineTaxable,
                TaxRate = item.TaxPercentage,
                TaxAmount = lineTax,
                CGSTAmount = lineCgst,
                SGSTAmount = lineSgst,
                IGSTAmount = lineIgst,
                ReturnAmount = lineAmount,
                Reason = reason,
                CompanyId = vendor.CompanyId
            };
            db.PurchaseReturnItems.Add(returnItem);

            var reversal = new PurchaseReturnItcReversal
            {
                PurchaseReturnId = purchaseReturn.Id,
                PurchaseReturnItemId = returnItem.Id,
                PurchaseInvoiceId = item.InvoiceId,
                PurchaseInvoiceItemId = item.Id,
                ReturnNumber = purchaseReturn.ReturnNumber,
                OriginalInvoiceNumber = sourceInvoice.InvoiceNumber,
                OnDate = returnDate,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? item.Barcode,
                HSNCode = item.HSNCode,
                TaxRate = item.TaxPercentage,
                ReturnedQuantity = requestedQuantity,
                TaxableAmount = lineTaxable,
                CGSTAmount = lineCgst,
                SGSTAmount = lineSgst,
                IGSTAmount = lineIgst,
                TaxAmount = lineTax,
                Status = "Posted",
                CompanyId = vendor.CompanyId,
                StoreGroupId = storeGroupId.Value,
                StoreId = storeId.Value
            };
            db.PurchaseReturnItcReversals.Add(reversal);
            taxPostings.Add(new PurchaseReturnTaxPosting(reversal.Id, reversal.ProductName, reversal.HSNCode, reversal.TaxAmount));

            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "PurchaseReturnOut",
                QuantityOut = requestedQuantity,
                CostPrice = stockSnapshot.AverageCost,
                MRP = stock.MRP,
                TaxRate = item.TaxPercentage,
                HSNCode = item.HSNCode ?? stock.HSNCode,
                SourceType = "PurchaseReturn",
                SourceId = purchaseReturn.Id,
                SourceNumber = purchaseReturn.ReturnNumber,
                Remarks = reason,
                OnDate = returnDate,
                CompanyId = vendor.CompanyId,
                StoreGroupId = storeGroupId.Value,
                StoreId = storeId.Value
            }, cancellationToken);

            returnedQuantity += requestedQuantity;
            taxableAmount += lineTaxable;
            taxAmount += lineTax;
            returnAmount += lineAmount;
            cgstAmount += lineCgst;
            sgstAmount += lineSgst;
            igstAmount += lineIgst;
            movementRemarks.Add($"{item.ProductName ?? item.Barcode} x {requestedQuantity:N2}");
            returnedLookup[key] = alreadyReturned + requestedQuantity;
        }

        returnAmount = Math.Round(taxableAmount + taxAmount, 2);
        if (returnedQuantity <= 0 || returnAmount <= 0)
        {
            return Results.BadRequest(new { message = "Selected return has no billable value." });
        }

        var debitNote = await CommercialEndpoints.CreateDebitNoteFromPurchaseReturnAsync(
            primaryInvoice,
            vendor,
            reason,
            storeGroupId.Value,
            storeId.Value,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            string.Join(", ", movementRemarks),
            db,
            cancellationToken);

        debitNote.Remarks = $"Goods return across {sourceInvoices.Count} invoice(s) ({string.Join(", ", distinctInvoiceNumbers)}): {string.Join(", ", movementRemarks)}";
        debitNote.SourceId = purchaseReturn.Id;
        debitNote.SourceNumber = purchaseReturn.ReturnNumber;
        purchaseReturn.Quantity = returnedQuantity;
        purchaseReturn.TaxableAmount = Math.Round(taxableAmount, 2);
        purchaseReturn.TaxAmount = Math.Round(taxAmount, 2);
        purchaseReturn.CGSTAmount = Math.Round(cgstAmount, 2);
        purchaseReturn.SGSTAmount = Math.Round(sgstAmount, 2);
        purchaseReturn.IGSTAmount = Math.Round(igstAmount, 2);
        purchaseReturn.ReturnAmount = Math.Round(returnAmount, 2);
        purchaseReturn.DebitNoteId = debitNote.Id;
        purchaseReturn.DebitNoteNumber = debitNote.NoteNumber;
        purchaseReturn.ItemCount = invoiceItems.Count;
        purchaseReturn.ItcReversalAmount = Math.Round(taxPostings.Sum(item => item.TaxAmount), 2);
        purchaseReturn.ItcReversalStatus = purchaseReturn.ItcReversalAmount == purchaseReturn.TaxAmount ? "Reconciled" : "Mismatch";

        var vendorFreightAmount = purchaseReturn.FreightBearer == "Vendor" ? purchaseReturn.FreightAmount : 0;
        var vendorFreightTaxAmount = purchaseReturn.FreightBearer == "Vendor" ? Math.Round(vendorFreightAmount * 0.05m, 2) : 0;
        purchaseReturn.FreightTaxAmount = vendorFreightTaxAmount;
        if (vendorFreightAmount > 0)
        {
            debitNote.TaxableAmount = Math.Round(debitNote.TaxableAmount + vendorFreightAmount, 2);
            debitNote.TaxAmount = Math.Round(debitNote.TaxAmount + vendorFreightTaxAmount, 2);
            debitNote.Amount = Math.Round(debitNote.Amount + vendorFreightAmount + vendorFreightTaxAmount, 2);
            debitNote.Remarks = $"{debitNote.Remarks} | Includes freight {vendorFreightAmount:N2} + GST {vendorFreightTaxAmount:N2} billed to vendor";
        }

        vendor.BillAmount = Math.Max(vendor.BillAmount - Math.Round(returnAmount + vendorFreightAmount + vendorFreightTaxAmount, 2), 0);

        foreach (var sourceInvoice in sourceInvoices)
        {
            var allItems = await db.PurchaseInvoiceItems.AsNoTracking()
                .Where(item => item.InvoiceId == sourceInvoice.Id)
                .Select(item => new { item.ProductId, item.Barcode, item.BilledQuantity })
                .ToListAsync(cancellationToken);
            var fullyReturned = allItems.All(item =>
            {
                returnedLookup.TryGetValue(VendorReturnQuantityKey(sourceInvoice.Id, item.ProductId, item.Barcode), out var quantity);
                return quantity >= item.BilledQuantity;
            });
            sourceInvoice.InvoiceStatus = fullyReturned ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded;
        }

        await accounting.PostPurchaseReturnAsync(
            purchaseReturn,
            primaryInvoice,
            vendor,
            debitNote.NoteNumber,
            storeGroupId.Value,
            storeId.Value,
            Math.Round(taxableAmount, 2),
            Math.Round(returnAmount, 2),
            taxPostings,
            reason,
            cancellationToken,
            vendorFreightAmount,
            vendorFreightTaxAmount);

        if (purchaseReturn.FreightBearer == "InHouse" && purchaseReturn.FreightAmount > 0 && freightEmployee is not null)
        {
            var freightLedger = await db.Ledgers.FirstOrDefaultAsync(
                ledger => ledger.CompanyId == vendor.CompanyId && ledger.Name == "Transport & Freight Charges",
                cancellationToken);
            if (freightLedger is null)
            {
                return Results.BadRequest(new { message = "Transport & Freight Charges ledger was not found for this company." });
            }

            var voucherResult = await accounting.SaveVoucherInCurrentTransactionAsync(
                new VoucherSaveRequest(
                    null,
                    string.Empty,
                    returnDate,
                    VoucherType.Expense,
                    vendor.Name,
                    $"Freight for goods return {purchaseReturn.ReturnNumber}",
                    purchaseReturn.FreightAmount,
                    $"Freight paid in-house against goods return {purchaseReturn.ReturnNumber} ({string.Join(", ", distinctInvoiceNumbers)}).",
                    null,
                    PaymentMode.Cash,
                    null,
                    false,
                    null,
                    freightLedger.Id,
                    freightEmployee.Id,
                    null,
                    vendor.CompanyId,
                    storeGroupId.Value,
                    storeId.Value),
                cancellationToken);

            var freightVoucher = await db.Vouchers.FirstOrDefaultAsync(voucher => voucher.Id == voucherResult.VoucherId, cancellationToken);
            purchaseReturn.FreightExpenseVoucherId = voucherResult.VoucherId;
            purchaseReturn.FreightExpenseVoucherNumber = freightVoucher?.VoucherNumber;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new VendorGoodsReturnResponse(
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            vendor.Id,
            vendor.Name,
            distinctInvoiceNumbers,
            debitNote.Id,
            debitNote.NoteNumber,
            returnedQuantity,
            Math.Round(taxableAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(returnAmount, 2),
            purchaseReturn.FreightAmount,
            purchaseReturn.FreightTaxAmount,
            purchaseReturn.FreightBearer,
            purchaseReturn.FreightExpenseVoucherNumber));
    }

    private static async Task<IResult> ReversePurchaseReturnAsync(
        Guid id,
        PurchaseReturnReversalRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        var purchaseReturn = await WorkspaceScope.ApplyTo(db.PurchaseReturns, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (purchaseReturn is null)
        {
            return Results.NotFound(new { message = "Purchase return was not found." });
        }

        if (!string.Equals(purchaseReturn.Status, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Conflict(new { message = "This return has already been reversed or cancelled." });
        }

        if (purchaseReturn.SettledAmount > 0)
        {
            return Results.Conflict(new { message = "This return's debit note has already been used in a vendor settlement and cannot be reversed." });
        }

        var invoice = await db.PurchaseInvoices.FirstOrDefaultAsync(item => item.Id == purchaseReturn.PurchaseInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return Results.BadRequest(new { message = "Original purchase invoice was not found." });
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == purchaseReturn.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Purchase vendor was not found." });
        }

        CommercialNote? debitNote = null;
        if (purchaseReturn.DebitNoteId.HasValue)
        {
            debitNote = await db.CommercialNotes.FirstOrDefaultAsync(item => item.Id == purchaseReturn.DebitNoteId.Value, cancellationToken);
            if (debitNote is not null && debitNote.IsAdjusted)
            {
                return Results.Conflict(new { message = "This return's debit note has already been used in a vendor settlement and cannot be reversed." });
            }
        }

        Voucher? freightVoucher = null;
        if (purchaseReturn.FreightExpenseVoucherId.HasValue)
        {
            freightVoucher = await db.Vouchers.FirstOrDefaultAsync(item => item.Id == purchaseReturn.FreightExpenseVoucherId.Value, cancellationToken);
            if (freightVoucher is not null && await db.CashVoucherConversions.AnyAsync(item => item.VoucherId == freightVoucher.Id, cancellationToken))
            {
                return Results.Conflict(new { message = "The freight expense voucher for this return has been converted and cannot be reversed." });
            }
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Purchase return reversed" : request.Reason.Trim();
        var reverseDate = DateTime.Now;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var returnItems = await db.PurchaseReturnItems.Where(item => item.PurchaseReturnId == purchaseReturn.Id).ToListAsync(cancellationToken);
        var affectedInvoiceIds = returnItems.Select(row => row.PurchaseInvoiceId).Distinct().ToList();
        if (affectedInvoiceIds.Count == 0)
        {
            affectedInvoiceIds.Add(purchaseReturn.PurchaseInvoiceId);
        }
        foreach (var item in returnItems)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == purchaseReturn.CompanyId &&
                stock.StoreGroupId == purchaseReturn.StoreGroupId &&
                stock.StoreId == purchaseReturn.StoreId &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode &&
                !stock.IsOFB,
                cancellationToken);

            if (stock is not null)
            {
                var snapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
                await stockLedger.PostAsync(stock, new StockMovement
                {
                    Barcode = stock.Barcode,
                    MovementType = "PurchaseReturnReversalIn",
                    QuantityIn = item.ReturnedQuantity,
                    CostPrice = snapshot.AverageCost,
                    MRP = stock.MRP,
                    TaxRate = item.TaxRate,
                    HSNCode = item.HSNCode ?? stock.HSNCode,
                    SourceType = "PurchaseReturnReversal",
                    SourceId = purchaseReturn.Id,
                    SourceNumber = purchaseReturn.ReturnNumber,
                    Remarks = reason,
                    OnDate = reverseDate,
                    CompanyId = purchaseReturn.CompanyId,
                    StoreGroupId = purchaseReturn.StoreGroupId,
                    StoreId = purchaseReturn.StoreId
                }, cancellationToken);
            }
        }
        db.PurchaseReturnItems.RemoveRange(returnItems);

        var itcReversals = await db.PurchaseReturnItcReversals.Where(item => item.PurchaseReturnId == purchaseReturn.Id).ToListAsync(cancellationToken);
        db.PurchaseReturnItcReversals.RemoveRange(itcReversals);

        var returnJournal = await db.JournalEntries.Include(entry => entry.Lines)
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseReturn" && entry.SourceId == purchaseReturn.Id, cancellationToken);
        if (returnJournal is not null)
        {
            db.JournalLines.RemoveRange(returnJournal.Lines ?? []);
            db.JournalEntries.Remove(returnJournal);
        }

        if (debitNote is not null)
        {
            db.CommercialNotes.Remove(debitNote);
        }

        var vendorFreightAmount = purchaseReturn.FreightBearer == "Vendor" ? purchaseReturn.FreightAmount : 0;
        var vendorFreightTaxAmount = purchaseReturn.FreightBearer == "Vendor" ? purchaseReturn.FreightTaxAmount : 0;
        vendor.BillAmount += Math.Round(purchaseReturn.ReturnAmount + vendorFreightAmount + vendorFreightTaxAmount, 2);

        if (freightVoucher is not null)
        {
            var voucherJournal = await db.JournalEntries.Include(entry => entry.Lines)
                .FirstOrDefaultAsync(entry => entry.SourceType == "Voucher" && entry.SourceId == freightVoucher.Id, cancellationToken);
            if (voucherJournal is not null)
            {
                db.JournalLines.RemoveRange(voucherJournal.Lines ?? []);
                db.JournalEntries.Remove(voucherJournal);
            }

            var voucherBankTransactions = await db.BankTransactions.Where(item => item.Reference == freightVoucher.VoucherNumber).ToListAsync(cancellationToken);
            if (voucherBankTransactions.Count > 0)
            {
                var bankTransactionIds = voucherBankTransactions.Select(item => item.Id).ToArray();
                db.BankStatementLines.RemoveRange(db.BankStatementLines.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)));
                db.BankTransactions.RemoveRange(voucherBankTransactions);
            }
            db.ChequeLogs.RemoveRange(db.ChequeLogs.Where(item => item.Narration == freightVoucher.VoucherNumber));

            db.Vouchers.Remove(freightVoucher);
        }

        purchaseReturn.Status = "Cancelled";
        purchaseReturn.Deleted = request.HardDelete;

        foreach (var affectedInvoiceId in affectedInvoiceIds)
        {
            var affectedInvoice = affectedInvoiceId == invoice.Id
                ? invoice
                : await db.PurchaseInvoices.FirstOrDefaultAsync(row => row.Id == affectedInvoiceId, cancellationToken);
            if (affectedInvoice is null || affectedInvoice.InvoiceStatus is not (InvoiceStatus.Refunded or InvoiceStatus.PartiallyRefunded))
            {
                continue;
            }

            var stillReturned = await db.PurchaseReturnItems.AsNoTracking()
                .Where(item => item.CompanyId == affectedInvoice.CompanyId && item.PurchaseInvoiceId == affectedInvoice.Id)
                .AnyAsync(cancellationToken);

            if (!stillReturned)
            {
                var paidAmount = await GetPaidAmountAsync(affectedInvoice.Id, db, cancellationToken);
                affectedInvoice.InvoiceStatus = ResolvePurchaseInvoiceStatus(affectedInvoice.BillAmount, paidAmount);
            }
            else
            {
                var allItems = await db.PurchaseInvoiceItems.AsNoTracking()
                    .Where(item => item.InvoiceId == affectedInvoice.Id)
                    .Select(item => new { item.ProductId, item.Barcode, item.BilledQuantity })
                    .ToListAsync(cancellationToken);
                var returnedLookup = await GetReturnedQuantityLookupAsync(affectedInvoice.Id, affectedInvoice.CompanyId, db, cancellationToken);
                var fullyReturned = allItems.All(item =>
                {
                    returnedLookup.TryGetValue(PurchaseReturnKey(item.ProductId, item.Barcode), out var quantity);
                    return quantity >= item.BilledQuantity;
                });
                affectedInvoice.InvoiceStatus = fullyReturned ? InvoiceStatus.Refunded : InvoiceStatus.PartiallyRefunded;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new PurchaseReturnReversalResponse(
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            purchaseReturn.Status,
            purchaseReturn.Deleted,
            invoice.Id,
            invoice.InvoiceStatus.ToString()));
    }

    private static async Task<PurchaseReceiptDto?> LoadReceiptAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoice.CompanyId, cancellationToken);
        var sourceJournal = await db.JournalEntries.AsNoTracking().FirstOrDefaultAsync(item => item.SourceType == "PurchaseInvoice" && item.SourceId == invoice.Id, cancellationToken);
        var resolvedStoreId = invoice.StoreId ?? sourceJournal?.StoreId;
        var storeName = resolvedStoreId.HasValue
            ? await db.Stores.AsNoTracking().Where(item => item.Id == resolvedStoreId.Value).Select(item => item.Name).FirstOrDefaultAsync(cancellationToken) ?? "Purchase Store"
            : "Purchase Store";

        var receiptItemRows = await db.PurchaseInvoiceItems.AsNoTracking()
            .Where(item => item.InvoiceId == invoice.Id)
            .Join(db.Products.AsNoTracking(), item => item.ProductId, product => product.Id, (item, product) => new { item, product })
            .ToListAsync(cancellationToken);

        var items = receiptItemRows.Select(row =>
        {
            var quantity = row.item.BilledQuantity <= 0 ? 0 : row.item.BilledQuantity;
            var basicRate = quantity <= 0 ? 0 : Math.Round(row.item.BasePrice / quantity, 2);
            var costPrice = quantity <= 0 ? 0 : Math.Round(row.item.Amount / quantity, 2);
            var discountBase = row.item.BasePrice + row.item.DiscountAmount;
            var discountRate = discountBase <= 0 ? 0 : Math.Round(row.item.DiscountAmount * 100 / discountBase, 2);
            return new PurchaseReceiptItemDto(
                row.item.ProductName ?? row.product.Name,
                row.item.Barcode,
                string.IsNullOrWhiteSpace(row.item.HSNCode) ? row.product.HSNCode : row.item.HSNCode,
                (row.item.Unit ?? row.product.Unit).ToString(),
                row.item.BilledQuantity,
                row.item.MRP,
                basicRate,
                costPrice,
                discountRate,
                row.item.DiscountAmount,
                row.item.BasePrice,
                row.item.TaxPercentage,
                row.item.TaxAmount,
                row.item.CGSTAmount,
                row.item.SGSTAmount,
                row.item.IGSTAmount,
                row.item.Amount);
        }).ToList();

        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(payment => payment.PurchaseInvoiceId == invoice.Id)
            .OrderBy(payment => payment.OnDate)
            .Select(payment => new PurchasePaymentDto(
                payment.Id,
                payment.OnDate,
                payment.Amount,
                payment.PaymentMode.ToString(),
                payment.ReferenceNumber,
                payment.VoucherId))
            .ToListAsync(cancellationToken);

        var paidAmount = payments.Sum(payment => payment.Amount);
        if (paidAmount <= 0)
        {
            paidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        }
        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            paidAmount = 0;
        }

        var importProofLookup = await GetPurchaseImportProofLookupAsync(new[] { invoice.Id }, context, db, cancellationToken);
        importProofLookup.TryGetValue(invoice.Id, out var importBatchId);

        return new PurchaseReceiptDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            invoice.OnDate,
            invoice.InwardDate,
            invoice.SupplierInvoiceDate,
            invoice.DueDate,
            company?.Name ?? "Garmetix",
            BuildCompanyAddress(company),
            company?.ContactNumber ?? string.Empty,
            company?.GSTIN ?? string.Empty,
            storeName,
            invoice.VendorId,
            invoice.VendorName ?? "Supplier",
            invoice.VendorGSTIN,
            invoice.MRP,
            invoice.DiscountAmount,
            invoice.NetAmount,
            invoice.TaxAmount,
            invoice.FrightAmount,
            invoice.RoundOff,
            invoice.BillAmount,
            paidAmount,
            Math.Max(invoice.BillAmount - paidAmount, 0),
            invoice.InvoiceStatus.ToString(),
            invoice.PaymentMode?.ToString() ?? "-",
            importBatchId != Guid.Empty,
            importBatchId == Guid.Empty ? null : importBatchId,
            items,
            payments);
    }

    private static async Task<IResult> CreateInwardAsync(
        PurchaseInwardRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        GstinLookupService gstinLookup,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.VendorName))
        {
            return Results.BadRequest(new { message = "Vendor name is required." });
        }

        if (request.Items.Count == 0)
        {
            return Results.BadRequest(new { message = "At least one purchase item is required." });
        }

        if (request.Items.Any(item => item.Quantity <= 0 || item.CostPrice < 0 || item.Mrp < 0))
        {
            return Results.BadRequest(new { message = "Quantity, cost price, and MRP must be valid." });
        }

        var purchaseStore = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking().Include(store => store.Company), context)
            .Where(store => store.Id == request.StoreId && store.CompanyId == request.CompanyId && store.StoreGroupId == request.StoreGroupId)
            .Select(store => new
            {
                CompanyGstin = store.Company != null ? store.Company.GSTIN : string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (purchaseStore is null)
        {
            return Results.BadRequest(new { message = "Selected purchase store is outside your access scope." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var vendorValidation = !string.IsNullOrWhiteSpace(request.VendorGstin)
            ? await gstinLookup.ValidatePartyAsync("Vendor", request.VendorGstin, request.VendorName, null, cancellationToken)
            : null;
        var vendor = await GetOrCreateVendorAsync(request, db, gstinLookup, vendorValidation, cancellationToken);
        var interState = IsInterStateSupply(purchaseStore.CompanyGstin, vendor.GSTIN);
        var inwardDate = request.InwardDate?.Date ?? DateTime.Today;
        var invoiceDate = request.SupplierInvoiceDate?.Date ?? inwardDate;

        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? await documentNumbers.NextPurchaseInvoiceAsync(request.CompanyId, request.StoreGroupId, request.StoreId, cancellationToken)
            : request.InvoiceNumber.Trim();
        // Inward numbers are always server generated so users cannot accidentally
        // duplicate or break the store/month/INW/series format.
        var inwardNumber = await documentNumbers.NextPurchaseInwardAsync(
            request.CompanyId, request.StoreGroupId, request.StoreId, inwardDate, cancellationToken);
        var invoiceId = Guid.NewGuid();

        var invoiceItems = new List<PurchaseInvoiceItem>();
        decimal grossMrp = 0;
        decimal discountAmount = 0;
        decimal taxableAmount = 0;
        decimal taxAmount = 0;
        decimal cgstAmount = 0;
        decimal sgstAmount = 0;
        decimal igstAmount = 0;
        decimal totalQuantity = 0;

        foreach (var requestItem in request.Items)
        {
            var product = await GetOrCreateProductAsync(request, requestItem, db, cancellationToken);
            if (product is null)
            {
                return Results.BadRequest(new { message = "Run quick setup and select product category, subcategory, and tax before purchasing new products." });
            }

            var tax = requestItem.TaxId.HasValue
                ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == requestItem.TaxId.Value, cancellationToken)
                : await db.Taxes.FirstOrDefaultAsync(item => item.CompositeRate == product.TaxRate && item.TaxType == product.TaxType, cancellationToken);
            tax ??= await db.Taxes.FirstOrDefaultAsync(cancellationToken);

            if (tax is null)
            {
                return Results.BadRequest(new { message = "Tax setup is required before purchase inward." });
            }

            var barcode = string.IsNullOrWhiteSpace(requestItem.Barcode) ? product.Barcode : requestItem.Barcode.Trim();
            var lineMrp = requestItem.Mrp * requestItem.Quantity;
            var lineDiscount = requestItem.DiscountAmount * requestItem.Quantity;
            var lineCost = requestItem.CostPrice * requestItem.Quantity;
            var lineNet = Math.Max(lineCost - lineDiscount, 0);
            var taxable = Math.Round(lineNet / (1 + (tax.CompositeRate / 100)), 2);
            var taxValue = Math.Round(taxable * (tax.CompositeRate / 100), 2);
            var lineAmount = taxable + taxValue;
            var split = SplitGst(taxValue, tax.TaxType, interState);
            var hsnCode = string.IsNullOrWhiteSpace(requestItem.HsnCode) ? product.HSNCode : requestItem.HsnCode.Trim();
            var unit = requestItem.ProductUnit ?? product.Unit;

            invoiceItems.Add(new PurchaseInvoiceItem
            {
                InvoiceId = invoiceId,
                ProductId = product.Id,
                Barcode = barcode,
                ProductName = product.Name,
                HSNCode = hsnCode,
                Unit = unit,
                ProductCategoryId = product.ProductCategoryId,
                ProductSubCategoryId = product.ProductSubCategoryId,
                MRP = requestItem.Mrp,
                DiscountAmount = lineDiscount,
                BasePrice = taxable,
                TaxPercentage = tax.CompositeRate,
                TaxAmount = taxValue,
                CGSTAmount = split.Cgst,
                SGSTAmount = split.Sgst,
                IGSTAmount = split.Igst,
                Amount = lineAmount,
                TaxType = interState ? TaxType.IGST : tax.TaxType,
                TaxId = tax.Id,
                BilledQuantity = requestItem.Quantity,
                CompanyId = request.CompanyId
            });

            await DocumentNumberGenerator.LockStockKeyAsync(db, request.CompanyId, request.StoreGroupId, request.StoreId, product.Id, barcode, cancellationToken);

            var stock = await WorkspaceScope.ApplyTo(db.Stocks, context).FirstOrDefaultAsync(item =>
                item.ProductId == product.Id &&
                item.Barcode == barcode &&
                item.StoreId == request.StoreId &&
                !item.IsOFB,
                cancellationToken);

            if (stock is null)
            {
                stock = new Stock
                {
                    ProductId = product.Id,
                    Barcode = barcode,
                    Unit = unit,
                    HSNCode = hsnCode,
                    PurchaseQty = 0,
                    CostPrice = 0,
                    MRP = requestItem.Mrp,
                    TaxRate = tax.CompositeRate,
                    TaxType = tax.TaxType,
                    TaxId = tax.Id,
                    IsOFB = false,
                    CompanyId = request.CompanyId,
                    StoreGroupId = request.StoreGroupId,
                    StoreId = request.StoreId
                };
                db.Stocks.Add(stock);
            }
            else
            {
                stock.MRP = requestItem.Mrp;
                stock.TaxRate = tax.CompositeRate;
                stock.TaxType = tax.TaxType;
                stock.TaxId = tax.Id;
                stock.HSNCode = string.IsNullOrWhiteSpace(hsnCode) ? stock.HSNCode : hsnCode;
                stock.Unit = unit;
            }

            await stockLedger.PostAsync(stock, new StockMovement
            {
                Barcode = stock.Barcode,
                MovementType = "PurchaseIn",
                QuantityIn = requestItem.Quantity,
                CostPrice = requestItem.CostPrice,
                MRP = requestItem.Mrp,
                TaxRate = tax.CompositeRate,
                HSNCode = hsnCode,
                SourceType = "PurchaseInvoice",
                SourceId = invoiceId,
                SourceNumber = invoiceNumber,
                Remarks = $"Purchase inward dated {inwardDate:yyyy-MM-dd}",
                OnDate = inwardDate,
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
            }, cancellationToken);

            product.MRP = requestItem.Mrp;
            product.TaxRate = tax.CompositeRate;
            product.TaxType = tax.TaxType;
            if (!string.IsNullOrWhiteSpace(hsnCode))
            {
                product.HSNCode = hsnCode;
            }
            product.Unit = unit;
            if (requestItem.ProductType.HasValue)
            {
                product.ProductType = requestItem.ProductType.Value;
            }
            if (requestItem.ProductGroup.HasValue)
            {
                product.ProductGroup = requestItem.ProductGroup.Value;
            }

            grossMrp += lineMrp;
            discountAmount += lineDiscount;
            taxableAmount += taxable;
            taxAmount += taxValue;
            cgstAmount += split.Cgst;
            sgstAmount += split.Sgst;
            igstAmount += split.Igst;
            totalQuantity += requestItem.Quantity;
        }

        var netAmount = taxableAmount + taxAmount + request.FrightAmount;
        var billAmount = Math.Round(netAmount, 0);
        var paidAmount = Math.Min(Math.Max(request.PaidAmount, 0), billAmount);

        if (request.OriginalInvoiceId.HasValue)
        {
            var originalInvoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .FirstOrDefaultAsync(item => item.Id == request.OriginalInvoiceId.Value, cancellationToken);
            if (originalInvoice is null)
            {
                return Results.BadRequest(new { message = "Original purchase invoice for replacement was not found." });
            }
            if (originalInvoice.CompanyId != request.CompanyId || originalInvoice.StoreGroupId != request.StoreGroupId || originalInvoice.StoreId != request.StoreId)
            {
                return Results.BadRequest(new { message = "Replacement inward must use the same company/store as the original purchase invoice." });
            }
            if (originalInvoice.InvoiceStatus == InvoiceStatus.Cancelled)
            {
                return Results.Conflict(new { message = "Original purchase invoice is already cancelled." });
            }
        }

        var invoice = new PurchaseInvoice
        {
            Id = invoiceId,
            InvoiceNumber = invoiceNumber,
            InwardNumber = inwardNumber,
            InwardDate = inwardDate,
            OnDate = invoiceDate,
            InvoiceType = InvoiceType.Regular,
            InvoiceStatus = paidAmount <= 0
                ? InvoiceStatus.Pending
                : paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid,
            MRP = grossMrp,
            BasePrice = taxableAmount,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            CGSTAmount = cgstAmount,
            SGSTAmount = sgstAmount,
            IGSTAmount = igstAmount,
            InterState = interState,
            NetAmount = taxableAmount + taxAmount,
            RoundOff = billAmount - netAmount,
            BillAmount = billAmount,
            Quantity = totalQuantity,
            ItemCount = invoiceItems.Count,
            PaymentMode = paidAmount > 0 ? request.PaymentMode : null,
            VendorId = vendor.Id,
            VendorName = vendor.Name,
            VendorGSTIN = vendor.GSTIN,
            FrightAmount = request.FrightAmount,
            SupplierInvoiceDate = request.SupplierInvoiceDate?.Date,
            DueDate = request.DueDate?.Date ?? invoiceDate.AddDays(45),
            StoreGroupId = request.StoreGroupId,
            StoreId = request.StoreId,
            CompanyId = request.CompanyId,
            OriginalInvoiceId = request.OriginalInvoiceId
        };

        if (!WorkspaceScope.CanWrite(invoice, context, out var invoiceScopeMessage))
        {
            return Results.BadRequest(new { message = invoiceScopeMessage ?? "Selected company is outside your access scope." });
        }

        db.PurchaseInvoices.Add(invoice);
        db.PurchaseInvoiceItems.AddRange(invoiceItems);
        if (request.OriginalInvoiceId.HasValue || request.ReplacementApprovalRequested)
        {
            InvoiceReplacementAudit.Add(
                db,
                context,
                "Requested",
                nameof(PurchaseInvoice),
                invoice.Id,
                "Purchase Invoice Replacement",
                invoice.InwardNumber,
                invoice.CompanyId,
                request.StoreGroupId,
                request.StoreId,
                string.IsNullOrWhiteSpace(request.ReplacementReason)
                    ? $"Revised inward {invoice.InwardNumber} created for replacement approval."
                    : request.ReplacementReason.Trim(),
                before: new { originalInvoiceId = request.OriginalInvoiceId },
                after: new { revisedInvoiceId = invoice.Id, revisedInwardNumber = invoice.InwardNumber, invoice.BillAmount });
        }
        if (paidAmount > 0)
        {
            db.PurchasePayments.Add(new PurchasePayment
            {
                PurchaseInvoiceId = invoice.Id,
                VendorId = vendor.Id,
                OnDate = inwardDate,
                Amount = paidAmount,
                PaymentMode = request.PaymentMode,
                BankAccountId = request.BankAccountId,
                ReferenceNumber = invoice.InvoiceNumber,
                Remarks = "Purchase inward payment",
                CompanyId = request.CompanyId,
                StoreGroupId = request.StoreGroupId,
                StoreId = request.StoreId
            });
        }

        vendor.BillCount += 1;
        vendor.BillAmount += billAmount;
        vendor.Paid += paidAmount;
        await accounting.PostPurchaseInvoiceAsync(invoice, vendor, paidAmount, request.StoreGroupId, request.StoreId, request.BankAccountId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"/api/purchase/invoices/{invoice.Id}/receipt", new PurchaseInwardResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InwardNumber,
            vendor.Id,
            invoice.BillAmount,
            paidAmount,
            invoice.ItemCount,
            invoice.Quantity,
            vendorValidation?.Alerts ?? Array.Empty<string>()));
    }



    private static InvoiceStatus ResolvePurchaseInvoiceStatus(decimal billAmount, decimal paidAmount)
    {
        if (paidAmount <= 0)
        {
            return InvoiceStatus.Pending;
        }

        return paidAmount >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
    }

    private static async Task<IResult> GetPurchasePaymentReconciliationAsync(HttpContext context, GarmetixDbContext db, int take = 50, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 10, 200);

        var activePaymentRows = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .Select(item => new
            {
                item.Id,
                item.VendorId,
                item.PurchaseInvoiceId,
                item.Amount,
                item.PaymentMode,
                item.OnDate,
                item.CreatedAt,
                item.VoucherId
            })
            .ToListAsync(cancellationToken);

        var vendorIds = activePaymentRows.Select(item => item.VendorId).Distinct().ToArray();
        var vendors = vendorIds.Length == 0
            ? new Dictionary<Guid, Vendor>()
            : await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
                .Where(item => vendorIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var vendorMismatches = activePaymentRows
            .GroupBy(item => item.VendorId)
            .Select(group =>
            {
                vendors.TryGetValue(group.Key, out var vendor);
                var activeTotal = group.Sum(item => item.Amount);
                var storedPaid = vendor?.Paid ?? 0m;
                var difference = Math.Round(storedPaid - activeTotal, 2);
                return new VendorPaymentVendorMismatchDto(
                    group.Key,
                    vendor?.Name ?? "Vendor",
                    storedPaid,
                    activeTotal,
                    difference);
            })
            .Where(item => item.Difference != 0m)
            .OrderByDescending(item => Math.Abs(item.Difference))
            .Take(take)
            .ToList();

        var invoiceIds = activePaymentRows
            .Where(item => item.PurchaseInvoiceId != Guid.Empty)
            .Select(item => item.PurchaseInvoiceId)
            .Distinct()
            .ToArray();
        var invoices = invoiceIds.Length == 0
            ? new Dictionary<Guid, PurchaseInvoice>()
            : await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .Where(item => invoiceIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var invoiceMismatches = activePaymentRows
            .Where(item => item.PurchaseInvoiceId != Guid.Empty)
            .GroupBy(item => item.PurchaseInvoiceId)
            .Select(group =>
            {
                if (!invoices.TryGetValue(group.Key, out var invoice))
                {
                    return null;
                }

                vendors.TryGetValue(invoice.VendorId, out var vendor);
                var activePaid = group.Sum(item => item.Amount);
                var expectedStatus = ResolvePurchaseInvoiceStatus(invoice.BillAmount, activePaid);
                var expectedMode = activePaid <= 0
                    ? null
                    : group.OrderByDescending(item => item.OnDate).ThenByDescending(item => item.CreatedAt).FirstOrDefault()?.PaymentMode;
                var storedMode = invoice.PaymentMode;
                var statusMismatch = invoice.InvoiceStatus != expectedStatus;
                var modeMismatch = storedMode != expectedMode;
                if (!statusMismatch && !modeMismatch)
                {
                    return null;
                }

                return new VendorPaymentInvoiceMismatchDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.VendorId,
                    vendor?.Name ?? "Vendor",
                    invoice.BillAmount,
                    activePaid,
                    invoice.InvoiceStatus.ToString(),
                    expectedStatus.ToString(),
                    storedMode?.ToString(),
                    expectedMode?.ToString());
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .OrderByDescending(item => Math.Abs(item.BillAmount - item.ActivePaymentTotal))
            .Take(take)
            .ToList();

        var deletedPaymentsWithVoucher = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => item.Deleted && item.VoucherId.HasValue)
            .Select(item => new { item.Id, item.VoucherId })
            .Take(take * 5)
            .ToListAsync(cancellationToken);
        var deletedVoucherIds = deletedPaymentsWithVoucher.Select(item => item.VoucherId!.Value).Distinct().ToArray();
        var activeDeletedVouchers = deletedVoucherIds.Length == 0
            ? new List<Voucher>()
            : await db.Vouchers.AsNoTracking()
                .Where(item => deletedVoucherIds.Contains(item.Id) && !item.Deleted)
                .Take(take)
                .ToListAsync(cancellationToken);

        var artifactMismatches = activeDeletedVouchers
            .Select(item => new VendorPaymentArtifactMismatchDto(
                item.Id,
                item.VoucherNumber,
                "Linked vendor payment is deleted but voucher is still active."))
            .ToList();

        var summary = new VendorPaymentReconciliationSummaryDto(
            vendorMismatches.Count,
            invoiceMismatches.Count,
            artifactMismatches.Count,
            vendorMismatches.Sum(item => Math.Abs(item.Difference)),
            invoiceMismatches.Sum(item => Math.Abs(item.BillAmount - item.ActivePaymentTotal)));

        return Results.Ok(new VendorPaymentReconciliationDto(
            summary,
            vendorMismatches,
            invoiceMismatches,
            artifactMismatches));
    }

    private static async Task<IResult> RepairPurchasePaymentReconciliationAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var activePaymentRows = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .Select(item => new
            {
                item.VendorId,
                item.PurchaseInvoiceId,
                item.Amount,
                item.PaymentMode,
                item.OnDate,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var vendorIds = activePaymentRows.Select(item => item.VendorId).Distinct().ToArray();
        var vendors = vendorIds.Length == 0
            ? new List<Vendor>()
            : await WorkspaceScope.ApplyTo(db.Vendors, context)
                .Where(item => vendorIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var vendorsRepaired = 0;
        foreach (var vendor in vendors)
        {
            var activeTotal = activePaymentRows.Where(item => item.VendorId == vendor.Id).Sum(item => item.Amount);
            if (Math.Round(vendor.Paid - activeTotal, 2) == 0m)
            {
                continue;
            }

            vendor.Paid = activeTotal;
            vendor.UpdatedAt = DateTime.UtcNow;
            vendorsRepaired++;
        }

        var invoiceIds = activePaymentRows
            .Where(item => item.PurchaseInvoiceId != Guid.Empty)
            .Select(item => item.PurchaseInvoiceId)
            .Distinct()
            .ToArray();
        var invoices = invoiceIds.Length == 0
            ? new List<PurchaseInvoice>()
            : await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
                .Where(item => invoiceIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var invoicesRepaired = 0;
        foreach (var invoice in invoices)
        {
            var invoicePayments = activePaymentRows
                .Where(item => item.PurchaseInvoiceId == invoice.Id)
                .OrderByDescending(item => item.OnDate)
                .ThenByDescending(item => item.CreatedAt)
                .ToList();
            var paid = invoicePayments.Sum(item => item.Amount);
            var expectedStatus = ResolvePurchaseInvoiceStatus(invoice.BillAmount, paid);
            var expectedMode = paid <= 0 ? null : invoicePayments.FirstOrDefault()?.PaymentMode;
            if (invoice.InvoiceStatus == expectedStatus && invoice.PaymentMode == expectedMode)
            {
                continue;
            }

            invoice.InvoiceStatus = expectedStatus;
            invoice.PaymentMode = expectedMode;
            invoice.UpdatedAt = DateTime.UtcNow;
            invoicesRepaired++;
        }

        var deletedPayments = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => item.Deleted && item.VoucherId.HasValue)
            .Select(item => new { item.VoucherId })
            .ToListAsync(cancellationToken);
        var voucherIds = deletedPayments.Select(item => item.VoucherId!.Value).Distinct().ToArray();
        var vouchers = voucherIds.Length == 0
            ? new List<Voucher>()
            : await db.Vouchers
                .Where(item => voucherIds.Contains(item.Id) && !item.Deleted)
                .ToListAsync(cancellationToken);

        var artifactsRepaired = 0;
        foreach (var voucher in vouchers)
        {
            voucher.Deleted = true;
            voucher.UpdatedAt = DateTime.UtcNow;
            await SoftDeleteVoucherBankArtifactsAsync(db, voucher.VoucherNumber, cancellationToken);

            var journals = await db.JournalEntries
                .Where(entry => entry.SourceType == "VendorPaymentVoucher" && entry.SourceId == voucher.Id && !entry.Deleted)
                .ToListAsync(cancellationToken);
            var journalIds = journals.Select(entry => entry.Id).ToArray();
            foreach (var journal in journals)
            {
                journal.Deleted = true;
                journal.UpdatedAt = DateTime.UtcNow;
            }

            if (journalIds.Length > 0)
            {
                var lines = await db.JournalLines
                    .Where(line => journalIds.Contains(line.JournalEntryId) && !line.Deleted)
                    .ToListAsync(cancellationToken);
                foreach (var line in lines)
                {
                    line.Deleted = true;
                    line.UpdatedAt = DateTime.UtcNow;
                }
            }

            artifactsRepaired++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new VendorPaymentReconciliationRepairResponse(
            vendorsRepaired,
            invoicesRepaired,
            artifactsRepaired,
            "Vendor payment reconciliation repair completed."));
    }

    private static async Task<PagedPurchasePaymentsDto> SearchPurchasePaymentsAsync(
        HttpContext context,
        GarmetixDbContext db,
        int? year,
        int? month,
        int page = 1,
        int pageSize = 50,
        string? q = null,
        string? paymentMode = null,
        Guid? vendorId = null,
        DateTime? from = null,
        DateTime? to = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Today;
        var selectedYear = year.GetValueOrDefault(now.Year);
        var selectedMonth = month.GetValueOrDefault(now.Month);
        if (selectedMonth < 1 || selectedMonth > 12)
        {
            selectedMonth = now.Month;
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 25, 200);

        var fromDate = from?.Date ?? new DateTime(selectedYear, selectedMonth, 1);
        var toDateExclusive = to?.Date.AddDays(1) ?? fromDate.AddMonths(1);
        var term = q?.Trim().ToLowerInvariant();

        var query = WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => item.OnDate >= fromDate && item.OnDate < toDateExclusive);

        if (!includeDeleted)
        {
            query = query.Where(item => !item.Deleted);
        }

        if (vendorId.HasValue)
        {
            query = query.Where(item => item.VendorId == vendorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paymentMode) && !paymentMode.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<PaymentMode>(paymentMode, true, out var parsedMode))
            {
                query = query.Where(item => item.PaymentMode == parsedMode);
            }
            else if (int.TryParse(paymentMode, out var paymentModeValue) && Enum.IsDefined(typeof(PaymentMode), paymentModeValue))
            {
                var mode = (PaymentMode)paymentModeValue;
                query = query.Where(item => item.PaymentMode == mode);
            }
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                (item.ReferenceNumber != null && item.ReferenceNumber.ToLower().Contains(term)) ||
                (item.Remarks != null && item.Remarks.ToLower().Contains(term)) ||
                db.Vendors.Any(vendor => vendor.Id == item.VendorId && vendor.Name.ToLower().Contains(term)) ||
                db.PurchaseInvoices.Any(invoice => invoice.Id == item.PurchaseInvoiceId &&
                    (invoice.InvoiceNumber.ToLower().Contains(term) || invoice.InwardNumber.ToLower().Contains(term))));
        }

        var total = await query.CountAsync(cancellationToken);
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Amount = group.Sum(item => item.Amount),
                CashAmount = group.Sum(item => item.PaymentMode == PaymentMode.Cash ? item.Amount : 0m),
                NonCashAmount = group.Sum(item => item.PaymentMode == PaymentMode.Cash ? 0m : item.Amount),
                ActiveCount = group.Count(item => !item.Deleted),
                DeletedCount = group.Count(item => item.Deleted)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var payments = await query
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await BuildPurchasePaymentDtosAsync(payments, db, cancellationToken);
        return new PagedPurchasePaymentsDto(
            items,
            total,
            page,
            pageSize,
            selectedYear,
            selectedMonth,
            totals?.Amount ?? 0m,
            totals?.CashAmount ?? 0m,
            totals?.NonCashAmount ?? 0m,
            totals?.ActiveCount ?? 0,
            totals?.DeletedCount ?? 0);
    }

    private static async Task<IReadOnlyList<PurchasePaymentRegisterDto>> GetRecentPurchasePaymentsAsync(HttpContext context, GarmetixDbContext db, int take = 100, CancellationToken cancellationToken = default)
    {
        var payments = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(take, 1, 300))
            .ToListAsync(cancellationToken);

        return await BuildPurchasePaymentDtosAsync(payments, db, cancellationToken);
    }

    private static async Task<IResult> GetPurchasePaymentAsync(Guid id, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var payment = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound(new { message = "Vendor payment was not found." });
        }

        var rows = await BuildPurchasePaymentDtosAsync([payment], db, cancellationToken);
        return Results.Ok(rows.First());
    }

    private static async Task<IResult> UpdatePurchasePaymentAsync(
        Guid id,
        UpdatePurchasePaymentRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        }
        if (request.PaymentMode != PaymentMode.Cash && (!request.BankAccountId.HasValue || request.BankAccountId == Guid.Empty))
        {
            return Results.BadRequest(new { message = "Select bank account for non-cash vendor payment." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var payment = await WorkspaceScope.ApplyTo(db.PurchasePayments, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound(new { message = "Vendor payment was not found." });
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == payment.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Vendor was not found for this payment." });
        }

        PurchaseInvoice? invoice = null;
        if (payment.PurchaseInvoiceId != Guid.Empty)
        {
            invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
                .FirstOrDefaultAsync(item => item.Id == payment.PurchaseInvoiceId, cancellationToken);
            if (invoice is null)
            {
                return Results.BadRequest(new { message = "Linked purchase invoice was not found." });
            }

            var paidExcludingThis = await db.PurchasePayments.AsNoTracking()
                .Where(item => item.PurchaseInvoiceId == invoice.Id && item.Id != payment.Id && !item.Deleted)
                .SumAsync(item => item.Amount, cancellationToken);
            var allowed = Math.Max(invoice.BillAmount - paidExcludingThis, 0);
            if (request.Amount > allowed)
            {
                return Results.BadRequest(new { message = $"Payment amount cannot exceed remaining purchase balance {allowed:N2}." });
            }
        }

        var onDate = request.OnDate?.Date ?? payment.OnDate.Date;
        payment.OnDate = onDate;
        payment.Amount = request.Amount;
        payment.PaymentMode = request.PaymentMode;
        payment.BankAccountId = request.PaymentMode == PaymentMode.Cash ? null : request.BankAccountId;
        payment.ReferenceNumber = NormalizeOptional(request.ReferenceNumber);
        payment.Remarks = NormalizeOptional(request.Remarks);
        payment.UpdatedAt = DateTime.UtcNow;

        Voucher? voucher = null;
        if (payment.VoucherId.HasValue)
        {
            voucher = await db.Vouchers.FirstOrDefaultAsync(item => item.Id == payment.VoucherId.Value && !item.Deleted, cancellationToken);
            if (voucher is not null)
            {
                voucher.OnDate = onDate;
                voucher.Amount = request.Amount;
                voucher.PaymentMode = request.PaymentMode;
                voucher.AccountNumber = request.PaymentMode == PaymentMode.Cash ? null : request.BankAccountId;
                voucher.SlipNumber = NormalizeOptional(request.ReferenceNumber);
                voucher.PaymentDetails = NormalizeOptional(request.PaymentDetails);
                voucher.Remarks = NormalizeOptional(request.Remarks);
                voucher.Particulars = string.IsNullOrWhiteSpace(request.PaymentDetails)
                    ? $"Vendor payment {voucher.VoucherNumber}"
                    : request.PaymentDetails.Trim();
                voucher.UpdatedAt = DateTime.UtcNow;

                if (request.PaymentMode == PaymentMode.Cash)
                {
                    await SoftDeleteVoucherBankArtifactsAsync(db, voucher.VoucherNumber, cancellationToken);
                }

                await accounting.PostVendorPaymentVoucherAsync(voucher, vendor, payment.StoreGroupId, payment.StoreId, voucher.AccountNumber, cancellationToken);
            }
        }

        await RecalculateVendorAndPurchasePaymentStateAsync(
            db,
            payment.VendorId,
            payment.PurchaseInvoiceId == Guid.Empty ? null : payment.PurchaseInvoiceId,
            request.PaymentMode,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var rows = await BuildPurchasePaymentDtosAsync([payment], db, cancellationToken);
        return Results.Ok(rows.First());
    }

    private static async Task<IResult> DeletePurchasePaymentAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var payment = await WorkspaceScope.ApplyTo(db.PurchasePayments, context)
            .FirstOrDefaultAsync(item => item.Id == id && !item.Deleted, cancellationToken);
        if (payment is null)
        {
            return Results.NotFound(new { message = "Vendor payment was not found." });
        }

        PurchaseInvoice? invoice = null;
        if (payment.PurchaseInvoiceId != Guid.Empty)
        {
            invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
                .FirstOrDefaultAsync(item => item.Id == payment.PurchaseInvoiceId, cancellationToken);
            if (invoice is null)
            {
                return Results.BadRequest(new { message = "Linked purchase invoice was not found." });
            }
        }

        payment.Deleted = true;
        payment.UpdatedAt = DateTime.UtcNow;

        if (payment.VoucherId.HasValue)
        {
            var voucher = await db.Vouchers.FirstOrDefaultAsync(item => item.Id == payment.VoucherId.Value, cancellationToken);
            if (voucher is not null)
            {
                voucher.Deleted = true;
                voucher.UpdatedAt = DateTime.UtcNow;

                var bankTransactions = await db.BankTransactions
                    .Where(item => item.Reference == voucher.VoucherNumber)
                    .ToListAsync(cancellationToken);
                foreach (var bankTransaction in bankTransactions)
                {
                    bankTransaction.Deleted = true;
                    bankTransaction.UpdatedAt = DateTime.UtcNow;
                }

                var bankTransactionIds = bankTransactions.Select(item => item.Id).ToArray();
                if (bankTransactionIds.Length > 0)
                {
                    var statementLines = await db.BankStatementLines
                        .Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value))
                        .ToListAsync(cancellationToken);
                    foreach (var line in statementLines)
                    {
                        line.Deleted = true;
                        line.UpdatedAt = DateTime.UtcNow;
                    }

                    var chequeLogs = await db.ChequeLogs
                        .Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value))
                        .ToListAsync(cancellationToken);
                    foreach (var cheque in chequeLogs)
                    {
                        cheque.Deleted = true;
                        cheque.UpdatedAt = DateTime.UtcNow;
                    }
                }

                var journals = await db.JournalEntries
                    .Where(entry => entry.SourceType == "VendorPaymentVoucher" && entry.SourceId == voucher.Id)
                    .ToListAsync(cancellationToken);
                var journalIds = journals.Select(entry => entry.Id).ToArray();
                foreach (var journal in journals)
                {
                    journal.Deleted = true;
                    journal.UpdatedAt = DateTime.UtcNow;
                }
                if (journalIds.Length > 0)
                {
                    var lines = await db.JournalLines.Where(line => journalIds.Contains(line.JournalEntryId)).ToListAsync(cancellationToken);
                    foreach (var line in lines)
                    {
                        line.Deleted = true;
                        line.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }
        }

        await RecalculateVendorAndPurchasePaymentStateAsync(
            db,
            payment.VendorId,
            payment.PurchaseInvoiceId == Guid.Empty ? null : payment.PurchaseInvoiceId,
            null,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new { id, deleted = true, message = "Vendor payment deleted and linked voucher/accounting entries were reversed from active views." });
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task RecalculateVendorAndPurchasePaymentStateAsync(
        GarmetixDbContext db,
        Guid vendorId,
        Guid? purchaseInvoiceId,
        PaymentMode? preferredPaymentMode,
        CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == vendorId, cancellationToken);
        if (vendor is not null)
        {
            vendor.Paid = await db.PurchasePayments.AsNoTracking()
                .Where(item => item.VendorId == vendorId && !item.Deleted)
                .SumAsync(item => item.Amount, cancellationToken);
            vendor.UpdatedAt = DateTime.UtcNow;
        }

        if (!purchaseInvoiceId.HasValue || purchaseInvoiceId.Value == Guid.Empty)
        {
            return;
        }

        var invoice = await db.PurchaseInvoices.FirstOrDefaultAsync(item => item.Id == purchaseInvoiceId.Value, cancellationToken);
        if (invoice is null)
        {
            return;
        }

        var activePayments = await db.PurchasePayments.AsNoTracking()
            .Where(item => item.PurchaseInvoiceId == invoice.Id && !item.Deleted)
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var paid = activePayments.Sum(item => item.Amount);
        invoice.PaymentMode = paid <= 0
            ? null
            : preferredPaymentMode ?? activePayments.FirstOrDefault()?.PaymentMode;
        invoice.InvoiceStatus = paid <= 0
            ? InvoiceStatus.Pending
            : paid >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        invoice.UpdatedAt = DateTime.UtcNow;
    }

    private static async Task SoftDeleteVoucherBankArtifactsAsync(GarmetixDbContext db, string voucherNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucherNumber))
        {
            return;
        }

        var bankTransactions = await db.BankTransactions
            .Where(item => item.Reference == voucherNumber && !item.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var bankTransaction in bankTransactions)
        {
            bankTransaction.Deleted = true;
            bankTransaction.UpdatedAt = DateTime.UtcNow;
        }

        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToArray();
        if (bankTransactionIds.Length == 0)
        {
            return;
        }

        var statementLines = await db.BankStatementLines
            .Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value) && !item.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var line in statementLines)
        {
            line.Deleted = true;
            line.UpdatedAt = DateTime.UtcNow;
        }

        var chequeLogs = await db.ChequeLogs
            .Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value) && !item.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var cheque in chequeLogs)
        {
            cheque.Deleted = true;
            cheque.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task<IReadOnlyList<PurchasePaymentRegisterDto>> BuildPurchasePaymentDtosAsync(IReadOnlyList<PurchasePayment> payments, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var invoiceIds = payments
            .Where(item => item.PurchaseInvoiceId != Guid.Empty)
            .Select(item => item.PurchaseInvoiceId)
            .Distinct()
            .ToArray();
        var vendorIds = payments.Select(item => item.VendorId).Distinct().ToArray();
        var voucherIds = payments.Where(item => item.VoucherId.HasValue).Select(item => item.VoucherId!.Value).Distinct().ToArray();

        var invoices = invoiceIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.PurchaseInvoices.AsNoTracking()
                .Where(item => invoiceIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.InvoiceNumber, cancellationToken);

        var vendors = vendorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Vendors.AsNoTracking()
                .Where(item => vendorIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var vouchers = voucherIds.Length == 0
            ? new Dictionary<Guid, Voucher>()
            : await db.Vouchers.AsNoTracking()
                .Where(item => voucherIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        return payments.Select(payment =>
        {
            invoices.TryGetValue(payment.PurchaseInvoiceId, out var invoiceNumber);
            vendors.TryGetValue(payment.VendorId, out var vendorName);
            var voucher = payment.VoucherId.HasValue && vouchers.TryGetValue(payment.VoucherId.Value, out var row) ? row : null;
            var isAdvance = payment.PurchaseInvoiceId == Guid.Empty || string.Equals(payment.AdjustmentSourceType, "VendorAdvance", StringComparison.OrdinalIgnoreCase);
            return new PurchasePaymentRegisterDto(
                payment.Id,
                payment.OnDate,
                vendorName ?? "Vendor",
                payment.VendorId,
                isAdvance ? (Guid?)null : payment.PurchaseInvoiceId,
                isAdvance ? "Advance" : invoiceNumber ?? "Purchase invoice",
                isAdvance ? "Advance" : "Invoice",
                payment.Amount,
                (int)payment.PaymentMode,
                payment.PaymentMode.ToString(),
                payment.BankAccountId,
                payment.ReferenceNumber,
                voucher?.PaymentDetails,
                payment.Remarks,
                payment.VoucherId,
                voucher?.VoucherNumber);
        }).ToList();
    }

    private static async Task<IResult> CreateVendorAdvancePaymentAsync(
        VendorAdvancePaymentRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Advance payment amount must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var vendor = await WorkspaceScope.ApplyTo(db.Vendors, context)
            .FirstOrDefaultAsync(item => item.Id == request.VendorId && item.Active, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Select a valid active vendor." });
        }

        var companyId = WorkspaceScope.ClaimGuid(context, "companyId") ?? vendor.CompanyId;
        var storeGroupId = WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;

        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            var store = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Where(item => item.CompanyId == companyId)
                .OrderBy(item => item.Name)
                .FirstOrDefaultAsync(cancellationToken);
            storeGroupId = store?.StoreGroupId ?? storeGroupId;
            storeId = store?.Id ?? storeId;
        }

        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select workspace store before recording vendor advance payment." });
        }

        var voucherNumber = await documentNumbers.NextVendorPaymentVoucherAsync(companyId, storeGroupId, storeId, cancellationToken);
        var particulars = string.IsNullOrWhiteSpace(request.PaymentDetails)
            ? $"Vendor advance payment to {vendor.Name}"
            : request.PaymentDetails.Trim();

        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            VoucherNumber = voucherNumber,
            OnDate = DateTime.Now,
            VoucherType = VoucherType.Payment,
            PartyName = vendor.Name,
            Particulars = particulars,
            Amount = request.Amount,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? "Vendor advance payment" : request.Remarks.Trim(),
            SlipNumber = string.IsNullOrWhiteSpace(request.SlipNumber) ? null : request.SlipNumber.Trim(),
            PaymentMode = request.PaymentMode,
            PaymentDetails = request.PaymentDetails,
            AccountNumber = request.BankAccountId,
            IsParty = true,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };

        if (!WorkspaceScope.CanWrite(voucher, context, out var scopeMessage))
        {
            return Results.BadRequest(new { message = scopeMessage ?? "Selected company/store is outside your access scope." });
        }

        var payment = new PurchasePayment
        {
            PurchaseInvoiceId = Guid.Empty,
            VendorId = vendor.Id,
            OnDate = DateTime.Now,
            Amount = request.Amount,
            PaymentMode = request.PaymentMode,
            BankAccountId = request.BankAccountId,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.SlipNumber) ? voucher.VoucherNumber : request.SlipNumber.Trim(),
            VoucherId = voucher.Id,
            AdjustmentSourceType = "VendorAdvance",
            AdjustmentSourceId = vendor.Id,
            Remarks = voucher.Remarks,
            CompanyId = companyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };

        db.Vouchers.Add(voucher);
        db.PurchasePayments.Add(payment);
        vendor.Paid += request.Amount;

        await accounting.PostVendorPaymentVoucherAsync(voucher, vendor, storeGroupId, storeId, request.BankAccountId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new VendorAdvancePaymentResponse(payment.Id, voucher.Id, voucher.VoucherNumber, vendor.Id, vendor.Name, request.Amount));
    }

    private static async Task<IResult> CreateVendorPaymentVoucherAsync(
        Guid id,
        VendorPaymentVoucherRequest request,
        HttpContext context,
        GarmetixDbContext db,
        DocumentNumberService documentNumbers,
        AccountingPostingService accounting,
        CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            return Results.BadRequest(new { message = "Payment amount must be greater than zero." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cannot pay a cancelled purchase invoice." });
        }

        var existingPaidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        var balanceAmount = Math.Max(invoice.BillAmount - existingPaidAmount, 0);
        if (balanceAmount <= 0)
        {
            return Results.Conflict(new { message = "Purchase invoice is already fully paid." });
        }

        if (request.Amount > balanceAmount)
        {
            return Results.BadRequest(new { message = $"Payment amount cannot exceed purchase balance {balanceAmount:N2}." });
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Could not determine purchase store for vendor payment." });
        }

        var storeAllowed = await WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .AnyAsync(store => store.Id == storeId && store.CompanyId == invoice.CompanyId && store.StoreGroupId == storeGroupId, cancellationToken);
        if (!storeAllowed)
        {
            return Results.BadRequest(new { message = "Selected purchase store is outside your access scope." });
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == invoice.VendorId, cancellationToken);
        if (vendor is null)
        {
            return Results.BadRequest(new { message = "Purchase vendor was not found." });
        }

        var paymentAmount = request.Amount;
        var voucherNumber = await documentNumbers.NextVendorPaymentVoucherAsync(invoice.CompanyId, storeGroupId, storeId, cancellationToken);
        var particulars = string.IsNullOrWhiteSpace(request.PaymentDetails)
            ? $"Vendor payment against purchase invoice {invoice.InvoiceNumber}"
            : request.PaymentDetails.Trim();

        var voucher = new Voucher
        {
            Id = Guid.NewGuid(),
            VoucherNumber = voucherNumber,
            OnDate = DateTime.Now,
            VoucherType = VoucherType.Payment,
            PartyName = vendor.Name,
            Particulars = particulars,
            Amount = paymentAmount,
            Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? $"Purchase invoice {invoice.InvoiceNumber}" : request.Remarks.Trim(),
            SlipNumber = string.IsNullOrWhiteSpace(request.SlipNumber) ? null : request.SlipNumber.Trim(),
            PaymentMode = request.PaymentMode,
            PaymentDetails = request.PaymentDetails,
            AccountNumber = request.BankAccountId,
            IsParty = true,
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };

        if (!WorkspaceScope.CanWrite(voucher, context, out var scopeMessage))
        {
            return Results.BadRequest(new { message = scopeMessage ?? "Selected company/store is outside your access scope." });
        }

        db.Vouchers.Add(voucher);
        db.PurchasePayments.Add(new PurchasePayment
        {
            PurchaseInvoiceId = invoice.Id,
            VendorId = vendor.Id,
            OnDate = DateTime.Now,
            Amount = paymentAmount,
            PaymentMode = request.PaymentMode,
            BankAccountId = request.BankAccountId,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.SlipNumber) ? voucher.VoucherNumber : request.SlipNumber.Trim(),
            VoucherId = voucher.Id,
            Remarks = voucher.Remarks,
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        });
        vendor.Paid += paymentAmount;

        var newPaidAmount = existingPaidAmount + paymentAmount;
        invoice.PaymentMode = request.PaymentMode;
        invoice.InvoiceStatus = newPaidAmount >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;

        await accounting.PostVendorPaymentVoucherAsync(voucher, vendor, storeGroupId, storeId, request.BankAccountId, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new VendorPaymentVoucherResponse(
            voucher.Id,
            voucher.VoucherNumber,
            invoice.Id,
            invoice.InvoiceNumber,
            paymentAmount,
            newPaidAmount,
            Math.Max(invoice.BillAmount - newPaidAmount, 0),
            invoice.InvoiceStatus.ToString()));
    }

    /// <summary>
    /// CM-08 Communication & Mail integration point - additive, opt-in, read-only over an
    /// already-committed payment voucher (never called from CreateVendorPaymentVoucherAsync/
    /// CreateVendorAdvancePaymentAsync). No dedicated vendor-payment-receipt PDF exists in this
    /// codebase (PurchasePdfDocument is invoice/inward-shaped), so this is an email-only
    /// confirmation for a first pass, matching the CM-08 research survey's scope call.
    /// </summary>
    private static async Task<IResult> SendVendorPaymentEmailAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        Garmetix.Api.Communication.BusinessNotificationService notifications,
        CancellationToken cancellationToken)
    {
        var voucher = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context).FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (voucher is null)
        {
            return Results.NotFound(new { message = "Payment voucher not found." });
        }

        var payment = await db.PurchasePayments.AsNoTracking().FirstOrDefaultAsync(p => p.VoucherId == id, cancellationToken);
        var vendor = payment is null ? null : await db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == payment.VendorId, cancellationToken);
        var invoice = payment?.PurchaseInvoiceId is { } invoiceId
            ? await db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            : null;

        var tokens = new Dictionary<string, string>
        {
            ["vendorName"] = vendor?.Name ?? voucher.PartyName ?? "Vendor",
            ["storeName"] = "Garmetix Store",
            ["amount"] = voucher.Amount.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-IN")),
            ["invoiceNumber"] = invoice?.InvoiceNumber ?? voucher.VoucherNumber,
            ["paymentDate"] = voucher.OnDate.ToString("dd MMM yyyy"),
            ["paymentMode"] = voucher.PaymentMode.ToString(),
        };

        var createdByUserId = Guid.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (Guid?)null;

        var result = await notifications.SendVendorPaymentEmailAsync(
            id, vendor?.Email ?? string.Empty, vendor?.Name, tokens,
            voucher.CompanyId, voucher.StoreGroupId, voucher.StoreId, createdByUserId, cancellationToken);

        return Results.Ok(new { result.Enqueued, result.SkipReason });
    }

    private static async Task<IResult> UpdatePurchaseInvoiceAsync(
        Guid id,
        UpdatePurchaseInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Cancelled purchase invoices cannot be edited." });
        }

        var invoiceNumber = request.InvoiceNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            var exists = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .AnyAsync(item => item.Id != id && item.CompanyId == invoice.CompanyId && item.InvoiceNumber == invoiceNumber, cancellationToken);
            if (exists)
            {
                return Results.Conflict(new { message = $"Purchase invoice number {invoiceNumber} already exists." });
            }
            invoice.InvoiceNumber = invoiceNumber;
        }

        var inwardNumber = request.InwardNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(inwardNumber))
        {
            var exists = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .AnyAsync(item => item.Id != id && item.CompanyId == invoice.CompanyId && item.InwardNumber == inwardNumber, cancellationToken);
            if (exists)
            {
                return Results.Conflict(new { message = $"Inward number {inwardNumber} already exists." });
            }
            invoice.InwardNumber = inwardNumber;
        }

        if (request.OnDate.HasValue) invoice.OnDate = request.OnDate.Value.Date;
        if (request.InwardDate.HasValue)
        {
            var newInwardDate = request.InwardDate.Value.Date;
            if (invoice.InwardDate.Date != newInwardDate)
            {
                var movements = await db.StockMovements
                    .Where(item => item.SourceType == "PurchaseInvoice" && item.SourceId == invoice.Id && !item.Deleted)
                    .ToListAsync(cancellationToken);
                foreach (var movement in movements)
                {
                    movement.OnDate = newInwardDate;
                    movement.UpdatedAt = DateTime.UtcNow;
                }
            }
            invoice.InwardDate = newInwardDate;
        }
        invoice.SupplierInvoiceDate = request.SupplierInvoiceDate?.Date;
        if (request.DueDate.HasValue) invoice.DueDate = request.DueDate.Value.Date;
        if (!string.IsNullOrWhiteSpace(request.VendorName)) invoice.VendorName = request.VendorName.Trim();
        invoice.VendorGSTIN = string.IsNullOrWhiteSpace(request.VendorGstin) ? null : request.VendorGstin.Trim().ToUpperInvariant();
        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { invoice.Id, invoice.InvoiceNumber, invoice.InwardNumber, invoice.OnDate, invoice.InwardDate, invoice.SupplierInvoiceDate, invoice.DueDate, invoice.VendorName, invoice.VendorGSTIN });
    }

    private static Task<IResult> DeletePurchaseInvoiceAsync(
        Guid id,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService numbering,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
        => CancelPurchaseAsync(
            id,
            new CancelPurchaseInvoiceRequest("Deleted/cancelled from purchase invoice register"),
            context,
            db,
            accounting,
            numbering,
            stockLedger,
            cancellationToken);

    internal static Task<IResult> CancelPurchaseForReplacementApprovalAsync(
        Guid id,
        CancelPurchaseInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService numbering,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
        => CancelPurchaseAsync(id, request, context, db, accounting, numbering, stockLedger, cancellationToken);

    private static async Task<IResult> CancelPurchaseAsync(
        Guid id,
        CancelPurchaseInvoiceRequest request,
        HttpContext context,
        GarmetixDbContext db,
        AccountingPostingService accounting,
        DocumentNumberService numbering,
        StockLedgerService stockLedger,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var invoice = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice is null)
        {
            return Results.NotFound(new { message = "Purchase invoice was not found." });
        }

        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return Results.Conflict(new { message = "Purchase invoice is already cancelled." });
        }

        if (invoice.InvoiceStatus is InvoiceStatus.PartiallyRefunded or InvoiceStatus.Refunded)
        {
            return Results.Conflict(new { message = "This purchase invoice already has item-wise purchase returns. Use the partial return screen for remaining quantities instead of full cancel." });
        }

        var sourceJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.SourceType == "PurchaseInvoice" && entry.SourceId == invoice.Id, cancellationToken);
        var storeGroupId = invoice.StoreGroupId ?? sourceJournal?.StoreGroupId ?? WorkspaceScope.ClaimGuid(context, "storeGroupId") ?? Guid.Empty;
        var storeId = invoice.StoreId ?? sourceJournal?.StoreId ?? WorkspaceScope.ClaimGuid(context, "storeId") ?? Guid.Empty;
        if (storeGroupId == Guid.Empty || storeId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Could not determine purchase store for stock reversal." });
        }

        var invoiceItems = await db.PurchaseInvoiceItems
            .Where(item => item.InvoiceId == invoice.Id)
            .ToListAsync(cancellationToken);

        var returnDate = DateTime.Today;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Purchase return/cancel" : request.Reason.Trim();
        var purchaseReturn = new PurchaseReturn
        {
            ReturnNumber = await numbering.NextPurchaseReturnAsync(invoice.CompanyId, storeGroupId, storeId, returnDate, cancellationToken),
            OnDate = returnDate,
            PurchaseInvoiceId = invoice.Id,
            OriginalInvoiceNumber = invoice.InvoiceNumber,
            OriginalInvoiceDate = invoice.OnDate,
            SupplierInvoiceDate = invoice.SupplierInvoiceDate,
            VendorId = invoice.VendorId,
            VendorName = invoice.VendorName ?? "Supplier",
            VendorGstin = invoice.VendorGSTIN,
            ReturnKind = "Cancellation",
            Status = "Posted",
            Reason = reason,
            Quantity = invoiceItems.Sum(item => item.BilledQuantity),
            TaxableAmount = Math.Round(invoice.BasePrice, 2),
            TaxAmount = Math.Round(invoice.TaxAmount, 2),
            CGSTAmount = Math.Round(invoice.CGSTAmount ?? 0, 2),
            SGSTAmount = Math.Round(invoice.SGSTAmount ?? 0, 2),
            IGSTAmount = Math.Round(invoice.IGSTAmount ?? 0, 2),
            ReturnAmount = Math.Round(invoice.BillAmount, 2),
            ItemCount = invoiceItems.Count,
            CompanyId = invoice.CompanyId,
            StoreGroupId = storeGroupId,
            StoreId = storeId
        };
        db.PurchaseReturns.Add(purchaseReturn);

        decimal reversedQuantity = 0;
        var taxPostings = new List<PurchaseReturnTaxPosting>();
        foreach (var item in invoiceItems)
        {
            var quantity = item.BilledQuantity <= 0 ? 1 : item.BilledQuantity;
            var taxResult = PurchaseReturnItcCalculator.Calculate(
                new PurchaseReturnTaxSource(
                    quantity,
                    item.BasePrice,
                    item.TaxAmount,
                    item.CGSTAmount ?? 0,
                    item.SGSTAmount ?? 0,
                    item.IGSTAmount ?? 0,
                    item.DiscountAmount),
                quantity);
            var returnItem = new PurchaseReturnItem
            {
                PurchaseReturnId = purchaseReturn.Id,
                PurchaseInvoiceId = invoice.Id,
                PurchaseInvoiceItemId = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? item.Barcode,
                Barcode = item.Barcode,
                HSNCode = item.HSNCode,
                Unit = item.Unit,
                ProductCategoryId = item.ProductCategoryId,
                ProductSubCategoryId = item.ProductSubCategoryId,
                PurchasedQuantity = item.BilledQuantity,
                PreviouslyReturnedQuantity = 0,
                ReturnedQuantity = item.BilledQuantity,
                MRP = item.MRP,
                UnitRate = Math.Round(item.BasePrice / quantity, 2),
                DiscountAmount = taxResult.DiscountAmount,
                TaxableAmount = taxResult.TaxableAmount,
                TaxRate = item.TaxPercentage,
                TaxAmount = taxResult.TaxAmount,
                CGSTAmount = taxResult.CgstAmount,
                SGSTAmount = taxResult.SgstAmount,
                IGSTAmount = taxResult.IgstAmount,
                ReturnAmount = taxResult.ReturnAmount,
                Reason = reason,
                CompanyId = invoice.CompanyId
            };
            db.PurchaseReturnItems.Add(returnItem);

            var reversal = new PurchaseReturnItcReversal
            {
                PurchaseReturnId = purchaseReturn.Id,
                PurchaseReturnItemId = returnItem.Id,
                PurchaseInvoiceId = invoice.Id,
                PurchaseInvoiceItemId = item.Id,
                ReturnNumber = purchaseReturn.ReturnNumber,
                OriginalInvoiceNumber = invoice.InvoiceNumber,
                OnDate = returnDate,
                ProductId = item.ProductId,
                ProductName = item.ProductName ?? item.Barcode,
                HSNCode = item.HSNCode,
                TaxRate = item.TaxPercentage,
                ReturnedQuantity = item.BilledQuantity,
                TaxableAmount = taxResult.TaxableAmount,
                CGSTAmount = taxResult.CgstAmount,
                SGSTAmount = taxResult.SgstAmount,
                IGSTAmount = taxResult.IgstAmount,
                TaxAmount = taxResult.TaxAmount,
                Status = "Posted",
                CompanyId = invoice.CompanyId,
                StoreGroupId = storeGroupId,
                StoreId = storeId
            };
            db.PurchaseReturnItcReversals.Add(reversal);
            taxPostings.Add(new PurchaseReturnTaxPosting(reversal.Id, reversal.ProductName, reversal.HSNCode, reversal.TaxAmount));

            await DocumentNumberGenerator.LockStockKeyAsync(db, invoice.CompanyId, storeGroupId, storeId, item.ProductId, item.Barcode, cancellationToken);

            var stock = await db.Stocks.FirstOrDefaultAsync(stock =>
                stock.CompanyId == invoice.CompanyId &&
                stock.StoreGroupId == storeGroupId &&
                stock.StoreId == storeId &&
                stock.ProductId == item.ProductId &&
                stock.Barcode == item.Barcode &&
                !stock.IsOFB,
                cancellationToken);

            if (stock is not null)
            {
                var stockSnapshot = await stockLedger.GetSnapshotAsync(stock, cancellationToken);
                await stockLedger.PostAsync(stock, new StockMovement
                {
                    Barcode = stock.Barcode,
                    MovementType = "PurchaseReturnOut",
                    QuantityOut = item.BilledQuantity,
                    CostPrice = stockSnapshot.AverageCost,
                    MRP = stock.MRP,
                    TaxRate = stock.TaxRate,
                    HSNCode = item.HSNCode ?? stock.HSNCode,
                    SourceType = "PurchaseReturn",
                    SourceId = purchaseReturn.Id,
                    SourceNumber = purchaseReturn.ReturnNumber,
                    Remarks = reason,
                    OnDate = returnDate,
                    CompanyId = invoice.CompanyId,
                    StoreGroupId = storeGroupId,
                    StoreId = storeId
                }, cancellationToken);
                reversedQuantity += item.BilledQuantity;
            }
        }

        var vendor = await db.Vendors.FirstOrDefaultAsync(item => item.Id == invoice.VendorId, cancellationToken);
        var originalPaidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        var originalPaymentMode = invoice.PaymentMode;
        var bankAccountId = await db.BankTransactions.AsNoTracking()
            .Where(item => item.CompanyId == invoice.CompanyId && item.Reference == $"PI-{invoice.InvoiceNumber}")
            .Select(item => (Guid?)item.BankAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (vendor is not null)
        {
            vendor.BillCount = Math.Max(vendor.BillCount - 1, 0);
            vendor.BillAmount = Math.Max(vendor.BillAmount - invoice.BillAmount, 0);
            vendor.Paid = Math.Max(vendor.Paid - originalPaidAmount, 0);
        }

        CommercialNote? debitNote = null;
        if (vendor is not null)
        {
            debitNote = await CommercialEndpoints.CreateDebitNoteFromPurchaseReturnAsync(
                invoice,
                vendor,
                reason,
                storeGroupId,
                storeId,
                purchaseReturn.TaxableAmount,
                purchaseReturn.TaxAmount,
                purchaseReturn.ReturnAmount,
                string.Join(", ", invoiceItems.Select(item => $"{item.ProductName ?? item.Barcode} x {item.BilledQuantity:N2}")),
                db,
                cancellationToken);
            debitNote.SourceId = purchaseReturn.Id;
            debitNote.SourceNumber = purchaseReturn.ReturnNumber;
            purchaseReturn.DebitNoteId = debitNote.Id;
            purchaseReturn.DebitNoteNumber = debitNote.NoteNumber;
        }

        purchaseReturn.ItcReversalAmount = RoundMoney(taxPostings.Sum(item => item.TaxAmount));
        purchaseReturn.ItcReversalStatus = purchaseReturn.ItcReversalAmount == purchaseReturn.TaxAmount ? "Reconciled" : "Mismatch";
        var cancellationJournal = await accounting.PostPurchaseInvoiceCancellationAsync(
            invoice,
            vendor,
            storeGroupId,
            storeId,
            originalPaidAmount,
            originalPaymentMode,
            bankAccountId,
            taxPostings,
            cancellationToken);
        purchaseReturn.JournalEntryId = cancellationJournal?.Id;
        foreach (var reversal in db.PurchaseReturnItcReversals.Local.Where(item => item.PurchaseReturnId == purchaseReturn.Id))
        {
            reversal.JournalEntryId = cancellationJournal?.Id;
        }

        // Keep original purchase values for audit and print history. Reversal is represented by status, stock movement, debit note, and accounting reversal.
        invoice.InvoiceStatus = InvoiceStatus.Cancelled;
        invoice.PaymentMode = null;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new CancelPurchaseInvoiceResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceStatus.ToString(),
            reversedQuantity,
            invoiceItems.Sum(item => item.Amount),
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber));
    }

    private static async Task<Vendor> GetOrCreateVendorAsync(
        PurchaseInwardRequest request,
        GarmetixDbContext db,
        GstinLookupService gstinLookup,
        PartyGstinValidationResponse? validation,
        CancellationToken cancellationToken)
    {
        var mobile = string.IsNullOrWhiteSpace(request.VendorMobileNumber) ? "NA" : request.VendorMobileNumber.Trim();
        var name = request.VendorName.Trim();
        var gstin = GstinLookupService.NormalizeGstin(request.VendorGstin);

        Vendor? vendor = null;
        if (request.VendorId.HasValue && request.VendorId.Value != Guid.Empty)
        {
            vendor = await db.Vendors.FirstOrDefaultAsync(
                item => item.Id == request.VendorId.Value && item.CompanyId == request.CompanyId,
                cancellationToken);
        }

        vendor ??= await db.Vendors.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId &&
                ((!string.IsNullOrWhiteSpace(gstin) && item.GSTIN == gstin) || item.MobileNumber == mobile || item.Name == name),
            cancellationToken);

        if (vendor is not null)
        {
            if (validation is not null)
            {
                gstinLookup.ApplyVerification(vendor, validation);
                if (!string.IsNullOrWhiteSpace(validation.Lookup.PrincipalAddress) && (string.IsNullOrWhiteSpace(vendor.Address) || vendor.Address == "Dumka"))
                {
                    vendor.Address = validation.Lookup.PrincipalAddress;
                }
            }
            else if (!string.IsNullOrWhiteSpace(gstin))
            {
                vendor.GSTIN = gstin;
            }

            return vendor;
        }

        vendor = new Vendor
        {
            Name = name,
            Address = validation?.Lookup.PrincipalAddress ?? "Dumka",
            City = "Dumka",
            ZipCode = "814101",
            MobileNumber = mobile,
            GSTIN = string.IsNullOrWhiteSpace(gstin) ? null : gstin,
            Active = true,
            CompanyId = request.CompanyId
        };

        if (validation is not null)
        {
            gstinLookup.ApplyVerification(vendor, validation);
        }

        db.Vendors.Add(vendor);
        return vendor;
    }

    private static async Task<Product?> GetOrCreateProductAsync(
        PurchaseInwardRequest request,
        PurchaseInwardItemRequest requestItem,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (requestItem.ProductId.HasValue)
        {
            return await db.Products.FirstOrDefaultAsync(item => item.Id == requestItem.ProductId.Value, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(requestItem.ProductName) || string.IsNullOrWhiteSpace(requestItem.Barcode))
        {
            return null;
        }

        var barcode = requestItem.Barcode.Trim();
        var existing = await db.Products.FirstOrDefaultAsync(
            item => item.CompanyId == request.CompanyId && item.Barcode == barcode,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var categoryId = requestItem.ProductCategoryId ?? await db.ProductCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var subCategoryId = requestItem.ProductSubCategoryId ?? await db.ProductSubCategories
            .Where(item => item.CompanyId == request.CompanyId)
            .Select(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var tax = requestItem.TaxId.HasValue
            ? await db.Taxes.FirstOrDefaultAsync(item => item.Id == requestItem.TaxId.Value, cancellationToken)
            : await db.Taxes.FirstOrDefaultAsync(cancellationToken);

        if (categoryId == Guid.Empty || subCategoryId == Guid.Empty || tax is null)
        {
            return null;
        }

        var product = new Product
        {
            Name = requestItem.ProductName.Trim(),
            Barcode = barcode,
            MRP = requestItem.Mrp,
            TaxRate = tax.CompositeRate,
            TaxType = tax.TaxType,
            HSNCode = string.IsNullOrWhiteSpace(requestItem.HsnCode) ? null : requestItem.HsnCode.Trim(),
            Unit = requestItem.ProductUnit ?? Unit.Pcs,
            ProductType = requestItem.ProductType ?? ProductType.Apparels,
            ProductGroup = requestItem.ProductGroup ?? ProductGroup.Shirting,
            ProductCategoryId = categoryId,
            ProductSubCategoryId = subCategoryId,
            CompanyId = request.CompanyId,
            StoreGroupId = request.StoreGroupId
        };

        db.Products.Add(product);
        return product;
    }

    private static async Task<Dictionary<Guid, Guid>> GetPurchaseImportProofLookupAsync(Guid[] invoiceIds, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (invoiceIds.Length == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var rows = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => item.PostedPurchaseInvoiceId.HasValue && invoiceIds.Contains(item.PostedPurchaseInvoiceId.Value))
            .OrderByDescending(item => item.PostedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new { PurchaseInvoiceId = item.PostedPurchaseInvoiceId!.Value, item.Id })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.PurchaseInvoiceId)
            .ToDictionary(group => group.Key, group => group.First().Id);
    }

    private static async Task<Dictionary<Guid, decimal>> GetPaidAmountLookupAsync(Guid[] invoiceIds, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (invoiceIds.Length == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var purchasePayments = await db.PurchasePayments.AsNoTracking()
            .Where(payment => invoiceIds.Contains(payment.PurchaseInvoiceId) && !payment.Deleted)
            .Select(payment => new { InvoiceId = payment.PurchaseInvoiceId, Amount = payment.Amount })
            .ToListAsync(cancellationToken);

        if (purchasePayments.Count > 0)
        {
            return purchasePayments.GroupBy(row => row.InvoiceId).ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
        }

        var rows = await db.JournalLines.AsNoTracking()
            .Where(line => !line.Deleted
                && line.JournalEntry != null
                && !line.JournalEntry.Deleted
                && line.JournalEntry.SourceType == "PurchaseInvoice"
                && line.JournalEntry.SourceId.HasValue
                && invoiceIds.Contains(line.JournalEntry.SourceId.Value)
                && line.Debit > 0
                && (line.Narration ?? string.Empty).Contains("Purchase payment"))
            .Select(line => new
            {
                InvoiceId = line.JournalEntry!.SourceId!.Value,
                Amount = line.Debit
            })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(row => row.InvoiceId).ToDictionary(group => group.Key, group => group.Sum(row => row.Amount));
    }

    private static async Task<decimal> GetPaidAmountAsync(Guid invoiceId, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var lookup = await GetPaidAmountLookupAsync([invoiceId], db, cancellationToken);
        return lookup.TryGetValue(invoiceId, out var paidAmount) ? paidAmount : 0;
    }

    private static async Task RecalculatePurchaseInvoiceStatusAsync(PurchaseInvoice invoice, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return;
        }

        var paidAmount = await GetPaidAmountAsync(invoice.Id, db, cancellationToken);
        var lastPaymentMode = paidAmount <= 0
            ? (PaymentMode?)null
            : await db.PurchasePayments.AsNoTracking()
                .Where(item => item.PurchaseInvoiceId == invoice.Id && !item.Deleted)
                .OrderByDescending(item => item.OnDate)
                .ThenByDescending(item => item.CreatedAt)
                .Select(item => (PaymentMode?)item.PaymentMode)
                .FirstOrDefaultAsync(cancellationToken);
        invoice.PaymentMode = lastPaymentMode;
        invoice.InvoiceStatus = paidAmount <= 0
            ? InvoiceStatus.Pending
            : paidAmount >= invoice.BillAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
        invoice.UpdatedAt = DateTime.UtcNow;
    }

    private static string BuildCompanyAddress(Garmetix.Core.Models.Stores.Company? company)
    {
        if (company is null)
        {
            return string.Empty;
        }

        return string.Join(", ", new[] { company.Address, company.City, company.State, company.ZipCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }


    private static bool IsInterStateSupply(string? companyGstin, string? partyGstin)
    {
        var companyState = GstStateCode(companyGstin);
        var partyState = GstStateCode(partyGstin);
        return companyState is not null
            && partyState is not null
            && !string.Equals(companyState, partyState, StringComparison.Ordinal);
    }

    private static string? GstStateCode(string? gstin)
    {
        var normalized = new string((gstin ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();
        return normalized.Length == 15 && normalized[..2].All(char.IsDigit)
            ? normalized[..2]
            : null;
    }

    private static (decimal Cgst, decimal Sgst, decimal Igst) SplitGst(decimal totalTax, TaxType taxType, bool interState = false)
    {
        totalTax = Math.Round(totalTax, 2);
        if (interState && taxType is TaxType.GST or TaxType.CGST or TaxType.SGST or TaxType.IGST)
        {
            return (0, 0, totalTax);
        }

        return taxType switch
        {
            TaxType.IGST => (0, 0, totalTax),
            TaxType.CGST => (totalTax, 0, 0),
            TaxType.SGST => (0, totalTax, 0),
            TaxType.GST => (Math.Round(totalTax / 2m, 2), totalTax - Math.Round(totalTax / 2m, 2), 0),
            _ => (0, 0, 0)
        };
    }

    private static async Task<Dictionary<string, decimal>> GetReturnedQuantityLookupAsync(
        Guid purchaseInvoiceId,
        Guid companyId,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var formalRows = await db.PurchaseReturnItems.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.PurchaseInvoiceId == purchaseInvoiceId)
            .GroupBy(item => new { item.ProductId, item.Barcode })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(item => item.ReturnedQuantity)
            })
            .ToListAsync(cancellationToken);

        // Before Stage 8C, a purchase return existed only as a stock movement linked
        // directly to its invoice. New movements link to the formal PurchaseReturn.
        var legacyRows = await db.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == companyId
                && movement.SourceType == "PurchaseReturn"
                && movement.SourceId == purchaseInvoiceId
                && movement.MovementType == "PurchaseReturnOut")
            .GroupBy(movement => new { movement.ProductId, movement.Barcode })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(item => item.QuantityOut)
            })
            .ToListAsync(cancellationToken);

        var result = formalRows.ToDictionary(
            row => PurchaseReturnKey(row.ProductId, row.Barcode),
            row => row.Quantity);
        foreach (var row in legacyRows)
        {
            var key = PurchaseReturnKey(row.ProductId, row.Barcode);
            result[key] = result.GetValueOrDefault(key) + row.Quantity;
        }

        return result;
    }

    private static async Task<Dictionary<string, decimal>> GetReturnedQuantityLookupForInvoicesAsync(
        IReadOnlyCollection<Guid> purchaseInvoiceIds,
        Guid companyId,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        if (purchaseInvoiceIds.Count == 0)
        {
            return new Dictionary<string, decimal>();
        }

        var formalRows = await db.PurchaseReturnItems.AsNoTracking()
            .Where(item => item.CompanyId == companyId && purchaseInvoiceIds.Contains(item.PurchaseInvoiceId))
            .GroupBy(item => new { item.PurchaseInvoiceId, item.ProductId, item.Barcode })
            .Select(group => new
            {
                group.Key.PurchaseInvoiceId,
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(item => item.ReturnedQuantity)
            })
            .ToListAsync(cancellationToken);

        var legacyRows = await db.StockMovements.AsNoTracking()
            .Where(movement => movement.CompanyId == companyId
                && movement.SourceType == "PurchaseReturn"
                && movement.SourceId.HasValue
                && purchaseInvoiceIds.Contains(movement.SourceId.Value)
                && movement.MovementType == "PurchaseReturnOut")
            .GroupBy(movement => new { PurchaseInvoiceId = movement.SourceId!.Value, movement.ProductId, movement.Barcode })
            .Select(group => new
            {
                group.Key.PurchaseInvoiceId,
                group.Key.ProductId,
                group.Key.Barcode,
                Quantity = group.Sum(item => item.QuantityOut)
            })
            .ToListAsync(cancellationToken);

        var result = formalRows.ToDictionary(
            row => VendorReturnQuantityKey(row.PurchaseInvoiceId, row.ProductId, row.Barcode),
            row => row.Quantity);
        foreach (var row in legacyRows)
        {
            var key = VendorReturnQuantityKey(row.PurchaseInvoiceId, row.ProductId, row.Barcode);
            result[key] = result.GetValueOrDefault(key) + row.Quantity;
        }

        return result;
    }

    private static string VendorReturnQuantityKey(Guid purchaseInvoiceId, Guid productId, string barcode)
        => $"{purchaseInvoiceId:N}|{productId:N}|{barcode.Trim().ToUpperInvariant()}";

    private static string PurchaseReturnKey(Guid productId, string barcode) => $"{productId:N}|{barcode.Trim().ToUpperInvariant()}";

    private static string PurchaseReturnPrintStatus(bool printed, int printCount)
        => printCount > 1 ? "Reprinted" : printed ? "Printed" : "Not Printed";

    private static IReadOnlyList<PurchaseEnumOptionDto> EnumOptions<TEnum>() where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(value => new PurchaseEnumOptionDto(Convert.ToInt32(value), value.ToString()))
            .GroupBy(option => option.Value)
            .Select(group => group.First())
            .ToList();

    private static string NormalizePdfFormat(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "a5" or "a5-one" => "a5",
        "thermal-2" or "2-inch" or "thermal2" => "thermal-2",
        "thermal-3" or "3-inch" or "thermal3" => "thermal-3",
        _ => "a4"
    };
}
