using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Validation;

public static class DataConsistencyRepairEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const decimal QuantityTolerance = 0.001m;

    private static readonly IReadOnlyList<DataRepairActionDto> Actions = new[]
    {
        new DataRepairActionDto(
            "BACKFILL_SALE_ITEM_SNAPSHOTS",
            "Backfill sale item snapshots",
            "Copies missing product name, HSN and unit snapshots into sales invoice items from Product/Stock. This improves old invoice print and HSN reports without changing invoice totals.",
            "Low",
            new[] { "SALE_ITEM_SNAPSHOT_MISSING" },
            false),
        new DataRepairActionDto(
            "BACKFILL_PURCHASE_ITEM_SNAPSHOTS",
            "Backfill purchase item snapshots",
            "Copies missing product name, HSN and unit snapshots into purchase invoice items from Product/Stock. This improves purchase print and ITC/HSN reports without changing invoice totals.",
            "Low",
            new[] { "PURCHASE_ITEM_SNAPSHOT_MISSING" },
            false),
        new DataRepairActionDto(
            "RECALCULATE_SALE_GST_HEADERS",
            "Recalculate sale GST headers from items",
            "Recalculates sale invoice NetAmount, TaxAmount, CGST, SGST, IGST, RoundOff and BillAmount from stored item rows.",
            "Medium",
            new[] { "SALE_TAX_MISMATCH", "SALE_CGST_MISMATCH", "SALE_SGST_MISMATCH", "SALE_IGST_MISMATCH" },
            true),
        new DataRepairActionDto(
            "RECALCULATE_PURCHASE_GST_HEADERS",
            "Recalculate purchase GST headers from items",
            "Recalculates purchase invoice NetAmount, TaxAmount, CGST, SGST, IGST, RoundOff and BillAmount from stored item rows plus freight amount.",
            "Medium",
            new[] { "PURCHASE_TAX_MISMATCH", "PURCHASE_CGST_MISMATCH", "PURCHASE_SGST_MISMATCH", "PURCHASE_IGST_MISMATCH" },
            true),
        new DataRepairActionDto(
            "SYNC_SALE_PAID_AMOUNT",
            "Sync sale paid amount from payment rows",
            "Sets sales invoice PaidAmount from InvoicePayment totals. Use when Stage 4D reports a sales payment mismatch.",
            "Medium",
            new[] { "SALE_PAYMENT_MISMATCH" },
            true),
        new DataRepairActionDto(
            "NORMALIZE_COMMERCIAL_NOTE_ADJUSTMENTS",
            "Normalize credit/debit note adjustment balances",
            "Clamps commercial note AdjustedAmount between zero and total amount, then updates IsAdjusted from the final balance.",
            "Low",
            new[] { "COMMERCIAL_NOTE_OVER_ADJUSTED", "CREDIT_NOTE_OVER_ADJUSTMENT", "DEBIT_NOTE_OVER_ADJUSTMENT" },
            false),
        new DataRepairActionDto(
            "NORMALIZE_CUSTOMER_ADVANCE_BALANCES",
            "Normalize customer advance balances",
            "Clamps advance receipt adjustment values and recalculates AvailableAmount as Amount minus AdjustedAmount.",
            "Low",
            new[] { "CUSTOMER_ADVANCE_MISMATCH", "CUSTOMER_ADVANCE_OVER_ADJUSTED" },
            false),
        new DataRepairActionDto(
            "MERGE_DUPLICATE_DOCUMENT_SEQUENCES",
            "Merge duplicate document sequence rows",
            "Keeps one sequence row per company/store/document/date, preserves the highest last number, and soft-deletes duplicate sequence rows.",
            "High",
            new[] { "DUPLICATE_DOCUMENT_SEQUENCE" },
            true),
        new DataRepairActionDto(
            "REBUILD_STOCK_QTY_FROM_LEDGER",
            "Rebuild stock quantities from movement ledger",
            "Sets Stock.PurchaseQty to movement QuantityIn total and Stock.SoldQty to movement QuantityOut total for rows that have stock movements. This should be used only after verifying movement history is complete.",
            "High",
            new[] { "STOCK_LEDGER_MISMATCH", "NEGATIVE_STOCK" },
            true)
    };

    public static IEndpointRouteBuilder MapDataConsistencyRepairEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/data-consistency/repairs")
            .WithTags("Data consistency repairs")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/actions", () => Actions);
        group.MapPost("/preview", PreviewAsync);
        group.MapPost("/apply", ApplyAsync).RequireAuthorization(GarmetixPolicies.Edit);

        return app;
    }

    private static async Task<IResult> PreviewAsync(DataRepairRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        return Results.Ok(await RunAsync(request, context, db, apply: false, cancellationToken));
    }

    private static async Task<IResult> ApplyAsync(DataRepairRequest request, HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var action = FindAction(request.ActionCode);
        if (action is null)
        {
            return Results.BadRequest(new { message = $"Unknown repair action '{request.ActionCode}'." });
        }

        if (action.RequiresConfirmation && !request.Confirm)
        {
            return Results.BadRequest(new { message = $"Repair action {action.Code} requires explicit confirmation." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await RunAsync(request with { Confirm = true }, context, db, apply: true, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<DataRepairResultDto> RunAsync(DataRepairRequest request, HttpContext context, GarmetixDbContext db, bool apply, CancellationToken cancellationToken)
    {
        var action = FindAction(request.ActionCode);
        if (action is null)
        {
            return new DataRepairResultDto(request.ActionCode, apply, 0, 0, Array.Empty<DataRepairChangeDto>(), $"Unknown repair action '{request.ActionCode}'.", DateTimeOffset.UtcNow);
        }

        var limit = NormalizeLimit(request.Limit);
        return action.Code switch
        {
            "BACKFILL_SALE_ITEM_SNAPSHOTS" => await BackfillSaleItemSnapshotsAsync(context, db, limit, apply, cancellationToken),
            "BACKFILL_PURCHASE_ITEM_SNAPSHOTS" => await BackfillPurchaseItemSnapshotsAsync(context, db, limit, apply, cancellationToken),
            "RECALCULATE_SALE_GST_HEADERS" => await RecalculateSaleGstHeadersAsync(context, db, limit, apply, cancellationToken),
            "RECALCULATE_PURCHASE_GST_HEADERS" => await RecalculatePurchaseGstHeadersAsync(context, db, limit, apply, cancellationToken),
            "SYNC_SALE_PAID_AMOUNT" => await SyncSalePaidAmountAsync(context, db, limit, apply, cancellationToken),
            "NORMALIZE_COMMERCIAL_NOTE_ADJUSTMENTS" => await NormalizeCommercialNotesAsync(context, db, limit, apply, cancellationToken),
            "NORMALIZE_CUSTOMER_ADVANCE_BALANCES" => await NormalizeCustomerAdvancesAsync(context, db, limit, apply, cancellationToken),
            "MERGE_DUPLICATE_DOCUMENT_SEQUENCES" => await MergeDuplicateDocumentSequencesAsync(context, db, limit, apply, cancellationToken),
            "REBUILD_STOCK_QTY_FROM_LEDGER" => await RebuildStockQuantitiesFromLedgerAsync(context, db, limit, apply, cancellationToken),
            _ => new DataRepairResultDto(action.Code, apply, 0, 0, Array.Empty<DataRepairChangeDto>(), $"Repair action '{action.Code}' is not implemented.", DateTimeOffset.UtcNow)
        };
    }

    private static async Task<DataRepairResultDto> BackfillSaleItemSnapshotsAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var items = await WorkspaceScope.ApplyTo(db.InvoiceItems, context)
            .Where(item => string.IsNullOrWhiteSpace(item.ProductName) || string.IsNullOrWhiteSpace(item.HSNCode) || !item.Unit.HasValue)
            .OrderBy(item => item.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var invoiceIds = items.Select(item => item.InvoiceId).Distinct().ToList();
        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(invoice => invoiceIds.Contains(invoice.Id))
            .Select(invoice => new { invoice.Id, invoice.StoreId, invoice.InvoiceNumber })
            .ToDictionaryAsync(invoice => invoice.Id, cancellationToken);

        var productIds = items.Select(item => item.ProductId).Distinct().ToList();
        var products = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.Name, product.HSNCode, product.Unit })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var item in items)
        {
            invoices.TryGetValue(item.InvoiceId, out var invoice);
            products.TryGetValue(item.ProductId, out var product);
            var stock = invoice is null
                ? null
                : await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
                    .Where(stock => stock.StoreId == invoice.StoreId && stock.ProductId == item.ProductId && stock.Barcode == item.Barcode)
                    .Select(stock => new { stock.HSNCode, stock.Unit })
                    .FirstOrDefaultAsync(cancellationToken);

            ApplySnapshotChanges(item, "InvoiceItem", invoice?.InvoiceNumber ?? item.Barcode, product?.Name, stock?.HSNCode ?? product?.HSNCode, stock?.Unit ?? product?.Unit, changes, apply);
        }

        return Result("BACKFILL_SALE_ITEM_SNAPSHOTS", apply, items.Count, changes, "Sale item snapshot backfill completed.");
    }

    private static async Task<DataRepairResultDto> BackfillPurchaseItemSnapshotsAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var items = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceItems, context)
            .Where(item => string.IsNullOrWhiteSpace(item.ProductName) || string.IsNullOrWhiteSpace(item.HSNCode) || !item.Unit.HasValue)
            .OrderBy(item => item.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var invoiceIds = items.Select(item => item.InvoiceId).Distinct().ToList();
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(invoice => invoiceIds.Contains(invoice.Id))
            .Select(invoice => new { invoice.Id, invoice.StoreId, invoice.InvoiceNumber })
            .ToDictionaryAsync(invoice => invoice.Id, cancellationToken);

        var productIds = items.Select(item => item.ProductId).Distinct().ToList();
        var products = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.Name, product.HSNCode, product.Unit })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var item in items)
        {
            invoices.TryGetValue(item.InvoiceId, out var invoice);
            products.TryGetValue(item.ProductId, out var product);
            var stock = invoice is null
                ? null
                : await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
                    .Where(stock => stock.StoreId == invoice.StoreId && stock.ProductId == item.ProductId && stock.Barcode == item.Barcode)
                    .Select(stock => new { stock.HSNCode, stock.Unit })
                    .FirstOrDefaultAsync(cancellationToken);

            ApplySnapshotChanges(item, "PurchaseInvoiceItem", invoice?.InvoiceNumber ?? item.Barcode, product?.Name, stock?.HSNCode ?? product?.HSNCode, stock?.Unit ?? product?.Unit, changes, apply);
        }

        return Result("BACKFILL_PURCHASE_ITEM_SNAPSHOTS", apply, items.Count, changes, "Purchase item snapshot backfill completed.");
    }

    private static async Task<DataRepairResultDto> RecalculateSaleGstHeadersAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .OrderBy(invoice => invoice.OnDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var items = await db.InvoiceItems.AsNoTracking()
                .Where(item => item.InvoiceId == invoice.Id)
                .ToListAsync(cancellationToken);
            if (items.Count == 0)
            {
                continue;
            }

            var net = items.Sum(item => item.BasePrice);
            var tax = items.Sum(item => item.TaxAmount);
            var cgst = items.Sum(item => item.CGSTAmount ?? 0);
            var sgst = items.Sum(item => item.SGSTAmount ?? 0);
            var igst = items.Sum(item => item.IGSTAmount ?? 0);
            var unroundedTotal = items.Sum(item => item.Amount);
            var bill = Math.Round(unroundedTotal, 0);
            var roundOff = bill - unroundedTotal;

            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.NetAmount), invoice.NetAmount, net, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.TaxAmount), invoice.TaxAmount, tax, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.CGSTAmount), invoice.CGSTAmount ?? 0, cgst, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.SGSTAmount), invoice.SGSTAmount ?? 0, sgst, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.IGSTAmount), invoice.IGSTAmount ?? 0, igst, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.RoundOff), invoice.RoundOff, roundOff, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.BillAmount), invoice.BillAmount, bill, changes, apply, AmountTolerance);
        }

        return Result("RECALCULATE_SALE_GST_HEADERS", apply, invoices.Count, changes, "Sale GST header recalculation completed.");
    }

    private static async Task<DataRepairResultDto> RecalculatePurchaseGstHeadersAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var invoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices, context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .OrderBy(invoice => invoice.OnDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var items = await db.PurchaseInvoiceItems.AsNoTracking()
                .Where(item => item.InvoiceId == invoice.Id)
                .ToListAsync(cancellationToken);
            if (items.Count == 0)
            {
                continue;
            }

            var net = items.Sum(item => item.BasePrice);
            var tax = items.Sum(item => item.TaxAmount);
            var cgst = items.Sum(item => item.CGSTAmount ?? 0);
            var sgst = items.Sum(item => item.SGSTAmount ?? 0);
            var igst = items.Sum(item => item.IGSTAmount ?? 0);
            var unroundedTotal = items.Sum(item => item.Amount) + invoice.FrightAmount;
            var bill = Math.Round(unroundedTotal, 0);
            var roundOff = bill - unroundedTotal;

            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.NetAmount), invoice.NetAmount, net, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.TaxAmount), invoice.TaxAmount, tax, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.CGSTAmount), invoice.CGSTAmount ?? 0, cgst, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.SGSTAmount), invoice.SGSTAmount ?? 0, sgst, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.IGSTAmount), invoice.IGSTAmount ?? 0, igst, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.RoundOff), invoice.RoundOff, roundOff, changes, apply, AmountTolerance);
            ChangeDecimal(invoice, "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.BillAmount), invoice.BillAmount, bill, changes, apply, AmountTolerance);
        }

        return Result("RECALCULATE_PURCHASE_GST_HEADERS", apply, invoices.Count, changes, "Purchase GST header recalculation completed.");
    }

    private static async Task<DataRepairResultDto> SyncSalePaidAmountAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices, context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .OrderBy(invoice => invoice.OnDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var paid = await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(payment => payment.InvoiceId == invoice.Id)
                .SumAsync(payment => payment.Amount, cancellationToken);
            ChangeDecimal(invoice, "Invoice", invoice.Id, invoice.InvoiceNumber, nameof(invoice.PaidAmount), invoice.PaidAmount, paid, changes, apply, AmountTolerance);
        }

        return Result("SYNC_SALE_PAID_AMOUNT", apply, invoices.Count, changes, "Sales paid amount sync completed.");
    }

    private static async Task<DataRepairResultDto> NormalizeCommercialNotesAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var notes = await WorkspaceScope.ApplyTo(db.CommercialNotes, context)
            .Where(note => note.AdjustedAmount < 0 || note.AdjustedAmount > note.Amount || note.IsAdjusted != (note.AdjustedAmount >= note.Amount))
            .OrderBy(note => note.OnDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var note in notes)
        {
            var adjusted = ClampDecimal(note.AdjustedAmount, 0, Math.Max(0, note.Amount));
            var isAdjusted = adjusted >= note.Amount - AmountTolerance;
            ChangeDecimal(note, "CommercialNote", note.Id, note.NoteNumber, nameof(note.AdjustedAmount), note.AdjustedAmount, adjusted, changes, apply, 0.01m);
            ChangeBool(note, "CommercialNote", note.Id, note.NoteNumber, nameof(note.IsAdjusted), note.IsAdjusted, isAdjusted, changes, apply);
        }

        return Result("NORMALIZE_COMMERCIAL_NOTE_ADJUSTMENTS", apply, notes.Count, changes, "Commercial note adjustment normalization completed.");
    }

    private static async Task<DataRepairResultDto> NormalizeCustomerAdvancesAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var receipts = await WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts, context)
            .Where(receipt => receipt.AdjustedAmount < 0 || receipt.AdjustedAmount > receipt.Amount || receipt.AvailableAmount != receipt.Amount - receipt.AdjustedAmount)
            .OrderBy(receipt => receipt.OnDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var receipt in receipts)
        {
            var adjusted = ClampDecimal(receipt.AdjustedAmount, 0, Math.Max(0, receipt.Amount));
            var available = Math.Max(0, receipt.Amount - adjusted);
            ChangeDecimal(receipt, "CustomerAdvanceReceipt", receipt.Id, receipt.ReceiptNumber, nameof(receipt.AdjustedAmount), receipt.AdjustedAmount, adjusted, changes, apply, 0.01m);
            ChangeDecimal(receipt, "CustomerAdvanceReceipt", receipt.Id, receipt.ReceiptNumber, nameof(receipt.AvailableAmount), receipt.AvailableAmount, available, changes, apply, 0.01m);
        }

        return Result("NORMALIZE_CUSTOMER_ADVANCE_BALANCES", apply, receipts.Count, changes, "Customer advance balance normalization completed.");
    }

    private static async Task<DataRepairResultDto> MergeDuplicateDocumentSequencesAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var sequences = await WorkspaceScope.ApplyTo(db.DocumentSequences, context)
            .OrderBy(sequence => sequence.SequenceDate)
            .ThenBy(sequence => sequence.DocumentType)
            .ToListAsync(cancellationToken);

        var scanned = 0;
        foreach (var group in sequences
            .GroupBy(sequence => new { sequence.CompanyId, sequence.StoreGroupId, sequence.StoreId, DocumentType = sequence.DocumentType.Trim().ToUpperInvariant(), Date = sequence.SequenceDate.Date })
            .Where(group => group.Count() > 1)
            .Take(limit))
        {
            scanned++;
            var keep = group.OrderByDescending(sequence => sequence.LastNumber).ThenBy(sequence => sequence.CreatedAt).First();
            var maxLastNumber = group.Max(sequence => sequence.LastNumber);
            ChangeInt(keep, "DocumentSequence", keep.Id, keep.DocumentType, nameof(keep.LastNumber), keep.LastNumber, maxLastNumber, changes, apply);

            foreach (var duplicate in group.Where(sequence => sequence.Id != keep.Id))
            {
                ChangeBool(duplicate, "DocumentSequence", duplicate.Id, duplicate.DocumentType, nameof(duplicate.Deleted), duplicate.Deleted, true, changes, apply);
            }
        }

        return Result("MERGE_DUPLICATE_DOCUMENT_SEQUENCES", apply, scanned, changes, "Duplicate document sequence merge completed.");
    }

    private static async Task<DataRepairResultDto> RebuildStockQuantitiesFromLedgerAsync(HttpContext context, GarmetixDbContext db, int limit, bool apply, CancellationToken cancellationToken)
    {
        var changes = new List<DataRepairChangeDto>();
        var stocks = await WorkspaceScope.ApplyTo(db.Stocks, context)
            .OrderBy(stock => stock.Barcode)
            .Take(limit)
            .ToListAsync(cancellationToken);

        foreach (var stock in stocks)
        {
            var movementTotals = await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
                .Where(movement => movement.StockId == stock.Id)
                .GroupBy(movement => movement.StockId)
                .Select(group => new
                {
                    QuantityIn = group.Sum(movement => movement.QuantityIn),
                    QuantityOut = group.Sum(movement => movement.QuantityOut)
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (movementTotals is null)
            {
                continue;
            }

            var difference = Math.Round((stock.PurchaseQty - stock.SoldQty) - (movementTotals.QuantityIn - movementTotals.QuantityOut), 3);
            if (Math.Abs(difference) <= QuantityTolerance)
            {
                continue;
            }

            ChangeDecimal(stock, "Stock", stock.Id, stock.Barcode, nameof(stock.PurchaseQty), stock.PurchaseQty, movementTotals.QuantityIn, changes, apply, QuantityTolerance);
            ChangeDecimal(stock, "Stock", stock.Id, stock.Barcode, nameof(stock.SoldQty), stock.SoldQty, movementTotals.QuantityOut, changes, apply, QuantityTolerance);
        }

        return Result("REBUILD_STOCK_QTY_FROM_LEDGER", apply, stocks.Count, changes, "Stock quantity rebuild from ledger completed.");
    }

    private static void ApplySnapshotChanges(InvoiceItem item, string entityType, string referenceNumber, string? productName, string? hsnCode, Unit? unit, List<DataRepairChangeDto> changes, bool apply)
    {
        if (string.IsNullOrWhiteSpace(item.ProductName) && !string.IsNullOrWhiteSpace(productName))
        {
            AddChange(changes, entityType, item.Id, referenceNumber, nameof(item.ProductName), item.ProductName, productName);
            if (apply) item.ProductName = productName;
        }

        if (string.IsNullOrWhiteSpace(item.HSNCode) && !string.IsNullOrWhiteSpace(hsnCode))
        {
            AddChange(changes, entityType, item.Id, referenceNumber, nameof(item.HSNCode), item.HSNCode, hsnCode);
            if (apply) item.HSNCode = hsnCode;
        }

        if (!item.Unit.HasValue && unit.HasValue)
        {
            AddChange(changes, entityType, item.Id, referenceNumber, nameof(item.Unit), null, unit.Value.ToString());
            if (apply) item.Unit = unit.Value;
        }
    }

    private static void ChangeDecimal(object entity, string entityType, Guid entityId, string? referenceNumber, string field, decimal current, decimal next, List<DataRepairChangeDto> changes, bool apply, decimal tolerance)
    {
        current = Math.Round(current, 2);
        next = Math.Round(next, 2);
        if (Math.Abs(current - next) <= tolerance)
        {
            return;
        }

        AddChange(changes, entityType, entityId, referenceNumber, field, current.ToString("0.##"), next.ToString("0.##"));
        if (!apply)
        {
            return;
        }

        SetProperty(entity, field, next);
    }

    private static void ChangeInt(object entity, string entityType, Guid entityId, string? referenceNumber, string field, int current, int next, List<DataRepairChangeDto> changes, bool apply)
    {
        if (current == next)
        {
            return;
        }

        AddChange(changes, entityType, entityId, referenceNumber, field, current.ToString(), next.ToString());
        if (apply) SetProperty(entity, field, next);
    }

    private static void ChangeBool(object entity, string entityType, Guid entityId, string? referenceNumber, string field, bool current, bool next, List<DataRepairChangeDto> changes, bool apply)
    {
        if (current == next)
        {
            return;
        }

        AddChange(changes, entityType, entityId, referenceNumber, field, current.ToString(), next.ToString());
        if (apply) SetProperty(entity, field, next);
    }

    private static void SetProperty(object entity, string field, object? value)
    {
        var property = entity.GetType().GetProperty(field);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(entity, value);
        }
    }

    private static void AddChange(List<DataRepairChangeDto> changes, string entityType, Guid entityId, string? referenceNumber, string field, string? before, string? after)
    {
        changes.Add(new DataRepairChangeDto(entityType, entityId, referenceNumber, field, before, after));
    }

    private static DataRepairResultDto Result(string actionCode, bool applied, int scannedRows, IReadOnlyList<DataRepairChangeDto> changes, string message)
    {
        return new DataRepairResultDto(actionCode, applied, scannedRows, changes.Select(change => change.EntityId).Distinct().Count(), changes, message, DateTimeOffset.UtcNow);
    }

    private static DataRepairActionDto? FindAction(string? actionCode)
    {
        return Actions.FirstOrDefault(action => string.Equals(action.Code, actionCode?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static int NormalizeLimit(int limit)
    {
        if (limit <= 0) return 100;
        return Math.Min(limit, 500);
    }

    private static decimal ClampDecimal(decimal value, decimal min, decimal max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
}
