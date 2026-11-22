using Garmetix.Api.Inventory;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

/// <summary>
/// Shared plumbing for editing a posted Sale invoice in place: guards (cancelled / return / has returns / locked period),
/// a re-date cascade (stock movements, payments, loyalty rows, bank references, ledger replay) and a "reset postings"
/// step that removes everything the invoice created so it can be re-posted from the corrected data.
/// The journal itself is re-posted by the caller through AccountingPostingService.PostSalesInvoiceAsync, which upserts
/// by (SourceType, SourceId) and so picks up the new date and number.
/// </summary>
public sealed class SalesInvoiceEditService(GarmetixDbContext db, StockLedgerService stockLedger)
{
    /// <summary>Returns an error message when the invoice may not be edited at all, otherwise null.</summary>
    public async Task<string?> CheckEditableAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (invoice.InvoiceStatus == InvoiceStatus.Cancelled)
        {
            return "Cancelled sales invoices cannot be edited.";
        }

        if (invoice.ReturnInvoice)
        {
            return "A return / credit note cannot be edited.";
        }

        var linked = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.OriginalInvoiceId == invoice.Id && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(item => item.InvoiceNumber)
            .Take(3)
            .ToListAsync(cancellationToken);
        if (linked.Count > 0)
        {
            return $"This invoice has returns / exchanges against it ({string.Join(", ", linked)}) and cannot be edited.";
        }

        return null;
    }

    /// <summary>Blocks a change when the sale date falls inside an active financial-year lock that locks sales or accounting.</summary>
    public async Task<string?> CheckPeriodOpenAsync(Guid companyId, Guid storeId, DateTime date, CancellationToken cancellationToken)
    {
        var day = date.Date;
        var lockRow = await db.FinancialYearLocks.AsNoTracking()
            .Where(item => item.CompanyId == companyId && item.Active
                && (item.LockSales || item.LockAccounting)
                && item.PeriodStart <= day && item.PeriodEnd >= day
                && (item.StoreId == null || item.StoreId == storeId))
            .Select(item => item.FinancialYear)
            .FirstOrDefaultAsync(cancellationToken);
        return lockRow is null ? null : $"{day:dd MMM yyyy} falls inside the locked financial year {lockRow}. Unlock it first.";
    }

    /// <summary>
    /// Removes items, payments, stock movements, bank / statement / cheque rows, journal and loyalty rows created by
    /// the invoice (the header and its digital bill stay) and rebuilds the touched stock rollups. Returns the loyalty
    /// points the invoice had earned so the caller can take them back from the customer.
    /// </summary>
    public async Task<decimal> ResetPostingsAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var invoiceNumber = invoice.InvoiceNumber;
        var companyId = invoice.CompanyId;
        var invoiceId = invoice.Id;
        var saleBankReference = $"SI-{invoiceNumber}";

        var bankTransactions = await db.BankTransactions
            .Where(item => item.CompanyId == companyId && item.Reference != null
                && (item.Reference == saleBankReference || item.Reference.StartsWith(saleBankReference + "-")))
            .ToListAsync(cancellationToken);
        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToList();
        var bankStatementLines = bankTransactionIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.BankStatementLine>()
            : await db.BankStatementLines.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)).ToListAsync(cancellationToken);
        var chequeLogs = bankTransactionIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.ChequeLog>()
            : await db.ChequeLogs.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)).ToListAsync(cancellationToken);

        var journalEntries = await db.JournalEntries
            .Where(item => item.CompanyId == companyId && item.SourceType == "SalesInvoice" && item.SourceId == invoiceId)
            .ToListAsync(cancellationToken);
        var journalEntryIds = journalEntries.Select(item => item.Id).ToList();
        var journalLines = journalEntryIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.JournalLine>()
            : await db.JournalLines.Where(item => journalEntryIds.Contains(item.JournalEntryId)).ToListAsync(cancellationToken);

        var items = await db.InvoiceItems.Where(item => item.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        var payments = await db.InvoicePayments.Where(item => item.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        var movements = await db.StockMovements
            .Where(item => item.CompanyId == companyId && item.SourceType == "SalesInvoice" && item.SourceId == invoiceId)
            .ToListAsync(cancellationToken);
        var loyalty = await db.LoyaltyPointLedgers
            .Where(item => item.CompanyId == companyId && item.SourceType == "SaleInvoice" && item.SourceId == invoiceId)
            .ToListAsync(cancellationToken);
        var earnedPoints = loyalty.Sum(item => item.PointsIn - item.PointsOut);

        var affectedStockIds = movements.Where(item => item.StockId.HasValue).Select(item => item.StockId!.Value).Distinct().ToList();

        db.BankStatementLines.RemoveRange(bankStatementLines);
        db.ChequeLogs.RemoveRange(chequeLogs);
        db.BankTransactions.RemoveRange(bankTransactions);
        db.JournalLines.RemoveRange(journalLines);
        db.JournalEntries.RemoveRange(journalEntries);
        db.InvoicePayments.RemoveRange(payments);
        db.InvoiceItems.RemoveRange(items);
        db.StockMovements.RemoveRange(movements);
        db.LoyaltyPointLedgers.RemoveRange(loyalty);

        // Sold value on the stock row was increased by each line when the sale was made; take it back.
        foreach (var item in items)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(row => row.ProductId == item.ProductId && row.Barcode == item.Barcode && row.StoreId == invoice.StoreId && !row.IsOFB, cancellationToken);
            if (stock is not null)
            {
                stock.SoldValue = Math.Max(0, stock.SoldValue - item.Amount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (affectedStockIds.Count > 0)
        {
            var stocks = await db.Stocks.Where(item => affectedStockIds.Contains(item.Id)).ToListAsync(cancellationToken);
            foreach (var stock in stocks)
            {
                await stockLedger.RebuildProjectionAsync(stock, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return earnedPoints;
    }

    /// <summary>
    /// Moves a posted sale to another date and/or number without touching its items or amounts: stock movements,
    /// payments recorded on the sale day, loyalty rows and bank references follow, and each affected stock's ledger is
    /// replayed so running balances stay correct. The caller re-posts the journal afterwards.
    /// </summary>
    public async Task RedateAsync(Invoice invoice, DateTime oldOnDate, string oldNumber, DateTime newOnDate, string newNumber, CancellationToken cancellationToken)
    {
        var dateChanged = oldOnDate != newOnDate;
        var numberChanged = !string.Equals(oldNumber, newNumber, StringComparison.Ordinal);
        if (!dateChanged && !numberChanged)
        {
            return;
        }

        var payments = await db.InvoicePayments.Where(item => item.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
        foreach (var payment in payments)
        {
            // Payments taken at the counter move with the sale; later collections stay put unless they would now
            // pre-date the sale.
            if (payment.OnDate.Date == oldOnDate.Date || payment.OnDate.Date < newOnDate.Date)
            {
                payment.OnDate = newOnDate.Date + payment.OnDate.TimeOfDay;
            }
        }

        var movements = await db.StockMovements
            .Where(item => item.CompanyId == invoice.CompanyId && item.SourceType == "SalesInvoice" && item.SourceId == invoice.Id)
            .ToListAsync(cancellationToken);
        foreach (var movement in movements)
        {
            movement.OnDate = newOnDate.Date + movement.OnDate.TimeOfDay;
            movement.SourceNumber = newNumber;
        }

        var loyalty = await db.LoyaltyPointLedgers
            .Where(item => item.CompanyId == invoice.CompanyId && item.SourceType == "SaleInvoice" && item.SourceId == invoice.Id)
            .ToListAsync(cancellationToken);
        foreach (var row in loyalty)
        {
            row.OnDate = newOnDate.Date + row.OnDate.TimeOfDay;
            row.SourceNumber = newNumber;
        }

        if (numberChanged)
        {
            var oldReference = $"SI-{oldNumber}";
            var bankTransactions = await db.BankTransactions
                .Where(item => item.CompanyId == invoice.CompanyId && item.Reference != null
                    && (item.Reference == oldReference || item.Reference.StartsWith(oldReference + "-")))
                .ToListAsync(cancellationToken);
            foreach (var transaction in bankTransactions)
            {
                transaction.Reference = $"SI-{newNumber}" + transaction.Reference![oldReference.Length..];
            }

            var digitalBills = await db.DigitalInvoices.Where(item => item.InvoiceId == invoice.Id && !item.Deleted).ToListAsync(cancellationToken);
            foreach (var bill in digitalBills)
            {
                bill.InvoiceNumber = newNumber;
            }
        }

        if (dateChanged)
        {
            var digitalBills = await db.DigitalInvoices.Where(item => item.InvoiceId == invoice.Id && !item.Deleted).ToListAsync(cancellationToken);
            foreach (var bill in digitalBills)
            {
                bill.InvoiceDate = newOnDate;
            }

            // The GST book date follows the sale date unless someone deliberately adjusted it from Invoice Books Adjustment.
            if (invoice.BookDateUpdatedAt is null)
            {
                invoice.BookDate = newOnDate.Date;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var stockIds = movements.Where(item => item.StockId.HasValue).Select(item => item.StockId!.Value).Distinct().ToList();
        if (stockIds.Count > 0)
        {
            var stocks = await db.Stocks.Where(item => stockIds.Contains(item.Id)).ToListAsync(cancellationToken);
            foreach (var stock in stocks)
            {
                await stockLedger.ReplayAsync(stock, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
