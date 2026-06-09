using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Validation;

public static class DataConsistencyEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const decimal QuantityTolerance = 0.001m;

    public static IEndpointRouteBuilder MapDataConsistencyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/data-consistency")
            .WithTags("Data consistency")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/summary", SummaryAsync);
        group.MapGet("/issues", IssuesAsync);
        group.MapGet("/csv", CsvAsync);

        return app;
    }

    private static async Task<DataConsistencySummaryDto> SummaryAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var issues = await RunChecksAsync(context, db, cancellationToken);
        return BuildSummary(issues, DateTimeOffset.UtcNow);
    }

    private static async Task<DataConsistencyRunDto> IssuesAsync(HttpContext context, GarmetixDbContext db, string? severity, string? area, CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var issues = await RunChecksAsync(context, db, cancellationToken);
        var filtered = FilterIssues(issues, severity, area).ToList();
        return new DataConsistencyRunDto(generatedAt, BuildSummary(filtered, generatedAt), filtered);
    }

    private static async Task<IResult> CsvAsync(HttpContext context, GarmetixDbContext db, string? severity, string? area, CancellationToken cancellationToken)
    {
        var issues = FilterIssues(await RunChecksAsync(context, db, cancellationToken), severity, area).ToList();
        var csv = new StringBuilder();
        csv.AppendLine("Severity,Area,CheckCode,Title,EntityType,EntityId,ReferenceNumber,CompanyId,StoreId,ExpectedValue,ActualValue,Difference,Description");
        foreach (var issue in issues)
        {
            csv.AppendLine(string.Join(',',
                Csv(issue.Severity),
                Csv(issue.Area),
                Csv(issue.CheckCode),
                Csv(issue.Title),
                Csv(issue.EntityType),
                Csv(issue.EntityId?.ToString()),
                Csv(issue.ReferenceNumber),
                Csv(issue.CompanyId?.ToString()),
                Csv(issue.StoreId?.ToString()),
                Csv(FormatDecimal(issue.ExpectedValue)),
                Csv(FormatDecimal(issue.ActualValue)),
                Csv(FormatDecimal(issue.Difference)),
                Csv(issue.Description)));
        }

        return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Garmetix-data-consistency-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    private static IEnumerable<DataConsistencyIssueDto> FilterIssues(IEnumerable<DataConsistencyIssueDto> issues, string? severity, string? area)
    {
        var query = issues;
        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(issue => string.Equals(issue.Severity, severity.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            query = query.Where(issue => string.Equals(issue.Area, area.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(issue => SeverityRank(issue.Severity))
            .ThenBy(issue => issue.Area)
            .ThenBy(issue => issue.CheckCode)
            .ThenBy(issue => issue.ReferenceNumber ?? string.Empty);
    }

    private static async Task<IReadOnlyList<DataConsistencyIssueDto>> RunChecksAsync(HttpContext context, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var issues = new List<DataConsistencyIssueDto>();

        await CheckInventoryAsync(context, db, issues, cancellationToken);
        await CheckDocumentsAsync(context, db, issues, cancellationToken);
        await CheckInvoicesAndGstAsync(context, db, issues, cancellationToken);
        await CheckPaymentsAsync(context, db, issues, cancellationToken);
        await CheckAccountingAsync(context, db, issues, cancellationToken);

        return issues
            .OrderBy(issue => SeverityRank(issue.Severity))
            .ThenBy(issue => issue.Area)
            .ThenBy(issue => issue.CheckCode)
            .ThenBy(issue => issue.ReferenceNumber ?? string.Empty)
            .ToList();
    }

    private static async Task CheckInventoryAsync(HttpContext context, GarmetixDbContext db, List<DataConsistencyIssueDto> issues, CancellationToken cancellationToken)
    {
        var stocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Select(stock => new
            {
                stock.Id,
                stock.CompanyId,
                stock.StoreGroupId,
                stock.StoreId,
                stock.ProductId,
                stock.Barcode,
                stock.HSNCode,
                stock.PurchaseQty,
                stock.SoldQty,
                CurrentStock = stock.PurchaseQty - stock.SoldQty
            })
            .ToListAsync(cancellationToken);

        foreach (var stock in stocks.Where(stock => stock.CurrentStock < -QuantityTolerance))
        {
            issues.Add(Issue("Critical", "Inventory", "NEGATIVE_STOCK", "Negative stock", $"Stock row for barcode {stock.Barcode} has negative current stock.", "Stock", stock.Id, stock.Barcode, stock.CompanyId, stock.StoreId, 0, stock.CurrentStock, stock.CurrentStock));
        }

        var stockIds = stocks.Select(stock => stock.Id).ToHashSet();
        var movementRows = await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(movement => movement.StockId.HasValue)
            .Select(movement => new
            {
                StockId = movement.StockId!.Value,
                movement.QuantityIn,
                movement.QuantityOut
            })
            .ToListAsync(cancellationToken);

        var movementBalanceByStock = movementRows
            .Where(movement => stockIds.Contains(movement.StockId))
            .GroupBy(movement => movement.StockId)
            .ToDictionary(group => group.Key, group => group.Sum(movement => movement.QuantityIn - movement.QuantityOut));

        foreach (var stock in stocks)
        {
            if (!movementBalanceByStock.TryGetValue(stock.Id, out var ledgerBalance))
            {
                continue;
            }

            var difference = Math.Round(stock.CurrentStock - ledgerBalance, 3);
            if (Math.Abs(difference) > QuantityTolerance)
            {
                issues.Add(Issue("Warning", "Inventory", "STOCK_LEDGER_MISMATCH", "Stock movement ledger mismatch", $"Stock row {stock.Barcode} current quantity does not match net stock movement quantity.", "Stock", stock.Id, stock.Barcode, stock.CompanyId, stock.StoreId, ledgerBalance, stock.CurrentStock, difference));
            }
        }

        var productIds = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var productIdSet = productIds.ToHashSet();
        foreach (var stock in stocks.Where(stock => !productIdSet.Contains(stock.ProductId)))
        {
            issues.Add(Issue("Critical", "Inventory", "STOCK_PRODUCT_MISSING", "Stock product missing", $"Stock row {stock.Barcode} points to a product that is not found in the current workspace.", "Stock", stock.Id, stock.Barcode, stock.CompanyId, stock.StoreId, null, null, null));
        }

        var products = await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
            .Select(product => new { product.Id, product.CompanyId, product.Barcode, product.Name, product.HSNCode })
            .ToListAsync(cancellationToken);

        foreach (var group in products.Where(product => !string.IsNullOrWhiteSpace(product.Barcode)).GroupBy(product => new { product.CompanyId, Barcode = product.Barcode.Trim().ToUpperInvariant() }).Where(group => group.Count() > 1))
        {
            var sample = group.First();
            issues.Add(Issue("Warning", "Inventory", "DUPLICATE_PRODUCT_BARCODE", "Duplicate product barcode", $"Barcode {sample.Barcode} is used by {group.Count()} product master rows.", "Product", sample.Id, sample.Barcode, sample.CompanyId, null, 1, group.Count(), group.Count() - 1));
        }

        foreach (var stock in stocks.Where(stock => string.IsNullOrWhiteSpace(stock.HSNCode)).Take(200))
        {
            issues.Add(Issue("Info", "Inventory", "STOCK_HSN_MISSING", "Stock HSN missing", $"Stock row {stock.Barcode} has no HSN code. This can weaken GST HSN reports if item snapshots are missing.", "Stock", stock.Id, stock.Barcode, stock.CompanyId, stock.StoreId, null, null, null));
        }
    }

    private static async Task CheckDocumentsAsync(HttpContext context, GarmetixDbContext db, List<DataConsistencyIssueDto> issues, CancellationToken cancellationToken)
    {
        var invoices = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Select(invoice => new { invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber })
            .ToListAsync(cancellationToken);
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_SALE_INVOICE", "Duplicate sale invoice number", "Invoice", invoices.Select(invoice => new NumberRow(invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber)));

        var purchaseInvoices = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Select(invoice => new { invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber, invoice.InwardNumber })
            .ToListAsync(cancellationToken);
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_PURCHASE_INVOICE", "Duplicate purchase invoice number", "PurchaseInvoice", purchaseInvoices.Select(invoice => new NumberRow(invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber)));
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_INWARD_NUMBER", "Duplicate inward number", "PurchaseInvoice", purchaseInvoices.Select(invoice => new NumberRow(invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InwardNumber)));

        foreach (var invoice in purchaseInvoices.Where(invoice => !invoice.StoreId.HasValue || invoice.StoreId == Guid.Empty))
        {
            issues.Add(Issue("Critical", "Documents", "PURCHASE_STORE_MISSING", "Purchase invoice store missing", $"Purchase invoice {invoice.InvoiceNumber} is not linked to a store.", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, null, null, null, null));
        }

        var vouchers = await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
            .Select(voucher => new { voucher.Id, voucher.CompanyId, voucher.StoreId, voucher.VoucherNumber })
            .ToListAsync(cancellationToken);
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_VOUCHER_NUMBER", "Duplicate voucher number", "Voucher", vouchers.Select(voucher => new NumberRow(voucher.Id, voucher.CompanyId, voucher.StoreId, voucher.VoucherNumber)));

        var cashVouchers = await WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context)
            .Select(voucher => new { voucher.Id, voucher.CompanyId, voucher.StoreId, voucher.VoucherNumber })
            .ToListAsync(cancellationToken);
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_CASH_VOUCHER_NUMBER", "Duplicate cash voucher number", "CashVoucher", cashVouchers.Select(voucher => new NumberRow(voucher.Id, voucher.CompanyId, voucher.StoreId, voucher.VoucherNumber)));

        var notes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Select(note => new { note.Id, note.CompanyId, note.StoreId, note.NoteNumber })
            .ToListAsync(cancellationToken);
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_COMMERCIAL_NOTE", "Duplicate credit/debit note number", "CommercialNote", notes.Select(note => new NumberRow(note.Id, note.CompanyId, note.StoreId, note.NoteNumber)));

        var advances = await WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts.AsNoTracking(), context)
            .Select(receipt => new { receipt.Id, receipt.CompanyId, receipt.StoreId, receipt.ReceiptNumber })
            .ToListAsync(cancellationToken);
        AddDuplicateNumberIssues(issues, "Documents", "DUPLICATE_ADVANCE_RECEIPT", "Duplicate advance receipt number", "CustomerAdvanceReceipt", advances.Select(receipt => new NumberRow(receipt.Id, receipt.CompanyId, receipt.StoreId, receipt.ReceiptNumber)));

        var sequences = await WorkspaceScope.ApplyTo(db.DocumentSequences.AsNoTracking(), context)
            .Select(sequence => new { sequence.Id, sequence.CompanyId, sequence.StoreId, sequence.StoreGroupId, sequence.DocumentType, sequence.SequenceDate })
            .ToListAsync(cancellationToken);
        foreach (var group in sequences.GroupBy(sequence => new { sequence.CompanyId, sequence.StoreGroupId, sequence.StoreId, sequence.DocumentType, Date = sequence.SequenceDate.Date }).Where(group => group.Count() > 1))
        {
            var sample = group.First();
            issues.Add(Issue("Critical", "Documents", "DUPLICATE_DOCUMENT_SEQUENCE", "Duplicate document sequence row", $"Document sequence {sample.DocumentType} for {sample.SequenceDate:yyyy-MM-dd} has {group.Count()} rows. This can cause duplicate numbers.", "DocumentSequence", sample.Id, sample.DocumentType, sample.CompanyId, sample.StoreId, 1, group.Count(), group.Count() - 1));
        }
    }

    private static async Task CheckInvoicesAndGstAsync(HttpContext context, GarmetixDbContext db, List<DataConsistencyIssueDto> issues, CancellationToken cancellationToken)
    {
        var sales = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.CompanyId,
                invoice.StoreId,
                invoice.InvoiceNumber,
                invoice.NetAmount,
                invoice.TaxAmount,
                invoice.CGSTAmount,
                invoice.SGSTAmount,
                invoice.IGSTAmount,
                invoice.BillAmount
            })
            .ToListAsync(cancellationToken);

        var saleItems = await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context)
            .Select(item => new
            {
                item.Id,
                item.InvoiceId,
                item.CompanyId,
                item.Barcode,
                item.ProductName,
                item.HSNCode,
                item.Unit,
                item.BasePrice,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.Amount
            })
            .ToListAsync(cancellationToken);

        var saleItemsByInvoice = saleItems.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        foreach (var invoice in sales)
        {
            if (!saleItemsByInvoice.TryGetValue(invoice.Id, out var items) || items.Count == 0)
            {
                issues.Add(Issue("Warning", "GST", "SALE_ITEM_MISSING", "Sale invoice has no items", $"Sale invoice {invoice.InvoiceNumber} has no invoice item rows.", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, null, 0, null));
                continue;
            }

            CheckAmountMismatch(issues, "GST", "SALE_TAX_MISMATCH", "Sale tax total mismatch", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.TaxAmount, items.Sum(item => item.TaxAmount));
            CheckAmountMismatch(issues, "GST", "SALE_CGST_MISMATCH", "Sale CGST total mismatch", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.CGSTAmount ?? 0, items.Sum(item => item.CGSTAmount ?? 0));
            CheckAmountMismatch(issues, "GST", "SALE_SGST_MISMATCH", "Sale SGST total mismatch", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.SGSTAmount ?? 0, items.Sum(item => item.SGSTAmount ?? 0));
            CheckAmountMismatch(issues, "GST", "SALE_IGST_MISMATCH", "Sale IGST total mismatch", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.IGSTAmount ?? 0, items.Sum(item => item.IGSTAmount ?? 0));
        }

        foreach (var item in saleItems.Where(item => string.IsNullOrWhiteSpace(item.ProductName) || string.IsNullOrWhiteSpace(item.HSNCode) || item.Unit is null).Take(200))
        {
            issues.Add(Issue("Warning", "GST", "SALE_ITEM_SNAPSHOT_MISSING", "Sale item snapshot incomplete", $"Sale item barcode {item.Barcode} is missing product-name, HSN, or unit snapshot.", "InvoiceItem", item.Id, item.Barcode, item.CompanyId, null, null, null, null));
        }

        var purchases = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(invoice => new
            {
                invoice.Id,
                invoice.CompanyId,
                invoice.StoreId,
                invoice.InvoiceNumber,
                invoice.InwardNumber,
                invoice.NetAmount,
                invoice.TaxAmount,
                invoice.CGSTAmount,
                invoice.SGSTAmount,
                invoice.IGSTAmount,
                invoice.BillAmount
            })
            .ToListAsync(cancellationToken);

        var purchaseItems = await WorkspaceScope.ApplyTo(db.PurchaseInvoiceItems.AsNoTracking(), context)
            .Select(item => new
            {
                item.Id,
                item.InvoiceId,
                item.CompanyId,
                item.Barcode,
                item.ProductName,
                item.HSNCode,
                item.Unit,
                item.BasePrice,
                item.TaxAmount,
                item.CGSTAmount,
                item.SGSTAmount,
                item.IGSTAmount,
                item.Amount
            })
            .ToListAsync(cancellationToken);

        var purchaseItemsByInvoice = purchaseItems.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        foreach (var invoice in purchases)
        {
            if (!purchaseItemsByInvoice.TryGetValue(invoice.Id, out var items) || items.Count == 0)
            {
                issues.Add(Issue("Warning", "GST", "PURCHASE_ITEM_MISSING", "Purchase invoice has no items", $"Purchase invoice {invoice.InvoiceNumber} has no invoice item rows.", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, null, 0, null));
                continue;
            }

            CheckAmountMismatch(issues, "GST", "PURCHASE_TAX_MISMATCH", "Purchase tax total mismatch", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.TaxAmount, items.Sum(item => item.TaxAmount));
            CheckAmountMismatch(issues, "GST", "PURCHASE_CGST_MISMATCH", "Purchase CGST total mismatch", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.CGSTAmount ?? 0, items.Sum(item => item.CGSTAmount ?? 0));
            CheckAmountMismatch(issues, "GST", "PURCHASE_SGST_MISMATCH", "Purchase SGST total mismatch", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.SGSTAmount ?? 0, items.Sum(item => item.SGSTAmount ?? 0));
            CheckAmountMismatch(issues, "GST", "PURCHASE_IGST_MISMATCH", "Purchase IGST total mismatch", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.IGSTAmount ?? 0, items.Sum(item => item.IGSTAmount ?? 0));
        }

        foreach (var item in purchaseItems.Where(item => string.IsNullOrWhiteSpace(item.ProductName) || string.IsNullOrWhiteSpace(item.HSNCode) || item.Unit is null).Take(200))
        {
            issues.Add(Issue("Warning", "GST", "PURCHASE_ITEM_SNAPSHOT_MISSING", "Purchase item snapshot incomplete", $"Purchase item barcode {item.Barcode} is missing product-name, HSN, or unit snapshot.", "PurchaseInvoiceItem", item.Id, item.Barcode, item.CompanyId, null, null, null, null));
        }
    }

    private static async Task CheckPaymentsAsync(HttpContext context, GarmetixDbContext db, List<DataConsistencyIssueDto> issues, CancellationToken cancellationToken)
    {
        var sales = await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(invoice => new { invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber, invoice.BillAmount, invoice.PaidAmount })
            .ToListAsync(cancellationToken);

        var salePaymentTotals = await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
            .GroupBy(payment => payment.InvoiceId)
            .Select(group => new { InvoiceId = group.Key, Amount = group.Sum(payment => payment.Amount) })
            .ToListAsync(cancellationToken);
        var salePaymentByInvoice = salePaymentTotals.ToDictionary(item => item.InvoiceId, item => item.Amount);

        foreach (var invoice in sales)
        {
            var posted = salePaymentByInvoice.GetValueOrDefault(invoice.Id, 0m);
            CheckAmountMismatch(issues, "Payments", "SALE_PAYMENT_MISMATCH", "Sale paid amount mismatch", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.PaidAmount, posted);
            if (invoice.PaidAmount - invoice.BillAmount > AmountTolerance)
            {
                issues.Add(Issue("Warning", "Payments", "SALE_OVERPAID", "Sale invoice appears overpaid", $"Sale invoice {invoice.InvoiceNumber} paid amount exceeds bill amount.", "Invoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.BillAmount, invoice.PaidAmount, invoice.PaidAmount - invoice.BillAmount));
            }
        }

        var purchasePayments = await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .GroupBy(payment => payment.PurchaseInvoiceId)
            .Select(group => new { PurchaseInvoiceId = group.Key, Amount = group.Sum(payment => payment.Amount) })
            .ToListAsync(cancellationToken);
        var purchasePaymentByInvoice = purchasePayments.ToDictionary(item => item.PurchaseInvoiceId, item => item.Amount);

        var purchases = await WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(invoice => invoice.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(invoice => new { invoice.Id, invoice.CompanyId, invoice.StoreId, invoice.InvoiceNumber, invoice.BillAmount })
            .ToListAsync(cancellationToken);

        foreach (var invoice in purchases)
        {
            var paid = purchasePaymentByInvoice.GetValueOrDefault(invoice.Id, 0m);
            if (paid - invoice.BillAmount > AmountTolerance)
            {
                issues.Add(Issue("Warning", "Payments", "PURCHASE_OVERPAID", "Purchase invoice appears overpaid", $"Purchase invoice {invoice.InvoiceNumber} payment allocation exceeds bill amount.", "PurchaseInvoice", invoice.Id, invoice.InvoiceNumber, invoice.CompanyId, invoice.StoreId, invoice.BillAmount, paid, paid - invoice.BillAmount));
            }
        }

        var notes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Select(note => new { note.Id, note.CompanyId, note.StoreId, note.NoteNumber, note.Amount, note.AdjustedAmount })
            .ToListAsync(cancellationToken);
        foreach (var note in notes.Where(note => note.AdjustedAmount - note.Amount > AmountTolerance))
        {
            issues.Add(Issue("Critical", "Payments", "COMMERCIAL_NOTE_OVERADJUSTED", "Credit/debit note over-adjusted", $"Commercial note {note.NoteNumber} adjusted amount exceeds note amount.", "CommercialNote", note.Id, note.NoteNumber, note.CompanyId, note.StoreId, note.Amount, note.AdjustedAmount, note.AdjustedAmount - note.Amount));
        }

        var advances = await WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts.AsNoTracking(), context)
            .Select(receipt => new { receipt.Id, receipt.CompanyId, receipt.StoreId, receipt.ReceiptNumber, receipt.Amount, receipt.AdjustedAmount, receipt.AvailableAmount })
            .ToListAsync(cancellationToken);
        foreach (var receipt in advances)
        {
            if (receipt.AdjustedAmount - receipt.Amount > AmountTolerance)
            {
                issues.Add(Issue("Critical", "Payments", "ADVANCE_OVERADJUSTED", "Customer advance over-adjusted", $"Advance receipt {receipt.ReceiptNumber} adjusted amount exceeds receipt amount.", "CustomerAdvanceReceipt", receipt.Id, receipt.ReceiptNumber, receipt.CompanyId, receipt.StoreId, receipt.Amount, receipt.AdjustedAmount, receipt.AdjustedAmount - receipt.Amount));
            }

            var expectedAvailable = Math.Round(receipt.Amount - receipt.AdjustedAmount, 2);
            if (Math.Abs(expectedAvailable - receipt.AvailableAmount) > AmountTolerance)
            {
                issues.Add(Issue("Warning", "Payments", "ADVANCE_AVAILABLE_MISMATCH", "Customer advance available amount mismatch", $"Advance receipt {receipt.ReceiptNumber} available amount does not equal amount minus adjusted amount.", "CustomerAdvanceReceipt", receipt.Id, receipt.ReceiptNumber, receipt.CompanyId, receipt.StoreId, expectedAvailable, receipt.AvailableAmount, receipt.AvailableAmount - expectedAvailable));
            }
        }
    }

    private static async Task CheckAccountingAsync(HttpContext context, GarmetixDbContext db, List<DataConsistencyIssueDto> issues, CancellationToken cancellationToken)
    {
        var entries = await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
            .Select(entry => new { entry.Id, entry.CompanyId, entry.StoreId, entry.EntryNumber, entry.SourceType })
            .ToListAsync(cancellationToken);
        var entryIds = entries.Select(entry => entry.Id).ToHashSet();

        var lines = await WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context)
            .Where(line => entryIds.Contains(line.JournalEntryId))
            .Select(line => new { line.JournalEntryId, line.Debit, line.Credit })
            .ToListAsync(cancellationToken);

        var lineTotals = lines.GroupBy(line => line.JournalEntryId)
            .ToDictionary(group => group.Key, group => new { Debit = group.Sum(line => line.Debit), Credit = group.Sum(line => line.Credit), Count = group.Count() });

        foreach (var entry in entries)
        {
            if (!lineTotals.TryGetValue(entry.Id, out var total) || total.Count == 0)
            {
                issues.Add(Issue("Warning", "Accounting", "JOURNAL_LINES_MISSING", "Journal entry has no lines", $"Journal entry {entry.EntryNumber} has no journal lines.", "JournalEntry", entry.Id, entry.EntryNumber, entry.CompanyId, entry.StoreId, null, 0, null));
                continue;
            }

            var difference = Math.Round(total.Debit - total.Credit, 2);
            if (Math.Abs(difference) > AmountTolerance)
            {
                issues.Add(Issue("Critical", "Accounting", "JOURNAL_UNBALANCED", "Journal entry is unbalanced", $"Journal entry {entry.EntryNumber} has debit/credit difference of {difference:0.00}.", "JournalEntry", entry.Id, entry.EntryNumber, entry.CompanyId, entry.StoreId, total.Debit, total.Credit, difference));
            }
        }
    }

    private static void AddDuplicateNumberIssues(List<DataConsistencyIssueDto> issues, string area, string code, string title, string entityType, IEnumerable<NumberRow> rows)
    {
        foreach (var group in rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Number))
            .GroupBy(row => new { row.CompanyId, row.StoreId, Number = row.Number.Trim().ToUpperInvariant() })
            .Where(group => group.Count() > 1))
        {
            var sample = group.First();
            issues.Add(Issue("Critical", area, code, title, $"Number {sample.Number} is used by {group.Count()} {entityType} rows in the same company/store scope.", entityType, sample.Id, sample.Number, sample.CompanyId, sample.StoreId, 1, group.Count(), group.Count() - 1));
        }
    }

    private static void CheckAmountMismatch(List<DataConsistencyIssueDto> issues, string area, string code, string title, string entityType, Guid entityId, string referenceNumber, Guid companyId, Guid? storeId, decimal expected, decimal actual)
    {
        expected = Math.Round(expected, 2);
        actual = Math.Round(actual, 2);
        var difference = Math.Round(actual - expected, 2);
        if (Math.Abs(difference) <= AmountTolerance)
        {
            return;
        }

        issues.Add(Issue("Warning", area, code, title, $"{title} for {referenceNumber}: expected {expected:0.00}, actual {actual:0.00}.", entityType, entityId, referenceNumber, companyId, storeId, expected, actual, difference));
    }

    private static DataConsistencySummaryDto BuildSummary(IReadOnlyList<DataConsistencyIssueDto> issues, DateTimeOffset generatedAt)
    {
        var sections = issues
            .GroupBy(issue => issue.Area)
            .OrderBy(group => group.Key)
            .Select(group => new DataConsistencySectionDto(
                group.Key,
                group.Count(),
                group.Count(issue => IsSeverity(issue, "Critical")),
                group.Count(issue => IsSeverity(issue, "Warning")),
                group.Count(issue => IsSeverity(issue, "Info"))))
            .ToList();

        return new DataConsistencySummaryDto(
            generatedAt,
            issues.Count,
            issues.Count(issue => IsSeverity(issue, "Critical")),
            issues.Count(issue => IsSeverity(issue, "Warning")),
            issues.Count(issue => IsSeverity(issue, "Info")),
            sections);
    }

    private static DataConsistencyIssueDto Issue(
        string severity,
        string area,
        string checkCode,
        string title,
        string description,
        string? entityType,
        Guid? entityId,
        string? referenceNumber,
        Guid? companyId,
        Guid? storeId,
        decimal? expectedValue,
        decimal? actualValue,
        decimal? difference)
    {
        return new DataConsistencyIssueDto(
            severity,
            area,
            checkCode,
            title,
            description,
            entityType,
            entityId,
            referenceNumber,
            companyId,
            storeId,
            RoundNullable(expectedValue),
            RoundNullable(actualValue),
            RoundNullable(difference));
    }

    private static int SeverityRank(string severity) => severity.ToUpperInvariant() switch
    {
        "CRITICAL" => 0,
        "WARNING" => 1,
        "INFO" => 2,
        _ => 3
    };

    private static bool IsSeverity(DataConsistencyIssueDto issue, string severity) => string.Equals(issue.Severity, severity, StringComparison.OrdinalIgnoreCase);

    private static decimal? RoundNullable(decimal? value) => value.HasValue ? Math.Round(value.Value, 2) : null;

    private static string? FormatDecimal(decimal? value) => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : null;

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private sealed record NumberRow(Guid Id, Guid CompanyId, Guid? StoreId, string? Number);
}
