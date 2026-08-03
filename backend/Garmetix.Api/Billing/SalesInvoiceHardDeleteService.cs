using Garmetix.Api.Inventory;
using Garmetix.Core.Models.Audit;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

/// <summary>
/// Physically removes a Sale invoice and everything created from it - items, payments, stock movements,
/// journal/bank/cheque postings, commercial notes, loyalty ledger rows - then rebuilds the affected stock
/// rows' cached PurchaseQty/SoldQty/CostPrice rollups from the (now-shorter) real movement history.
/// Extracted so both the single-invoice Admin "hard delete" endpoint and the Vyapar Import "Undo Batch"
/// action share one correctness-tested removal path instead of drifting apart.
/// </summary>
public sealed class SalesInvoiceHardDeleteService(GarmetixDbContext db, StockLedgerService stockLedger)
{
    public async Task<AdminHardDeleteSaleResponse> HardDeleteAsync(
        Invoice invoice,
        bool deleteAudit,
        CancellationToken cancellationToken)
    {
        var invoiceNumber = invoice.InvoiceNumber;
        var companyId = invoice.CompanyId;
        var invoiceId = invoice.Id;

        var saleBankReference = $"SI-{invoiceNumber}";
        var saleCancelBankReference = $"SIC-{invoiceNumber}";
        var saleReturnBankReference = $"SR-{invoiceNumber}";
        var saleExchangeBankReference = $"SX-{invoiceNumber}";

        var bankTransactions = await db.BankTransactions
            .Where(item => item.CompanyId == companyId && item.Reference != null &&
                (item.Reference == saleBankReference || item.Reference.StartsWith(saleBankReference + "-") ||
                 item.Reference == saleCancelBankReference || item.Reference.StartsWith(saleCancelBankReference + "-") ||
                 item.Reference == saleReturnBankReference || item.Reference.StartsWith(saleReturnBankReference + "-") ||
                 item.Reference == saleExchangeBankReference || item.Reference.StartsWith(saleExchangeBankReference + "-")))
            .ToListAsync(cancellationToken);
        var bankTransactionIds = bankTransactions.Select(item => item.Id).ToList();

        var bankStatementLines = bankTransactionIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.BankStatementLine>()
            : await db.BankStatementLines.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)).ToListAsync(cancellationToken);
        var chequeLogs = bankTransactionIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.ChequeLog>()
            : await db.ChequeLogs.Where(item => item.BankTransactionId.HasValue && bankTransactionIds.Contains(item.BankTransactionId.Value)).ToListAsync(cancellationToken);

        var journalEntries = await db.JournalEntries
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoiceId) ||
                 item.ReferenceNumber == $"SI-{invoiceNumber}" ||
                 item.ReferenceNumber == $"SIC-{invoiceNumber}" ||
                 item.ReferenceNumber == $"SR-{invoiceNumber}" ||
                 item.ReferenceNumber == $"SX-{invoiceNumber}"))
            .ToListAsync(cancellationToken);
        var journalEntryIds = journalEntries.Select(item => item.Id).ToList();
        var journalLines = journalEntryIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.JournalLine>()
            : await db.JournalLines.Where(item => journalEntryIds.Contains(item.JournalEntryId)).ToListAsync(cancellationToken);

        var invoiceItems = await db.InvoiceItems.Where(item => item.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        var invoicePayments = await db.InvoicePayments.Where(item => item.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        var cardPayments = await db.CardPayments.Where(item => item.InvoiceId == invoiceId).ToListAsync(cancellationToken);
        var stockMovements = await db.StockMovements
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoiceId) ||
                 item.SourceNumber == invoiceNumber ||
                 item.SourceNumber == $"SI-{invoiceNumber}" ||
                 item.SourceNumber == $"SIC-{invoiceNumber}"))
            .ToListAsync(cancellationToken);
        var commercialNotes = await db.CommercialNotes
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoiceId) || item.SourceNumber == invoiceNumber))
            .ToListAsync(cancellationToken);
        var loyaltyLedgers = await db.LoyaltyPointLedgers
            .Where(item => item.CompanyId == companyId &&
                ((item.SourceId.HasValue && item.SourceId.Value == invoiceId) || item.SourceNumber == invoiceNumber))
            .ToListAsync(cancellationToken);
        var auditEntries = deleteAudit
            ? await db.AuditLogEntries.Where(item => item.EntityId == invoiceId || item.Reference == invoiceNumber || item.Reference == $"SI-{invoiceNumber}" || item.Reference == $"SIC-{invoiceNumber}").ToListAsync(cancellationToken)
            : new List<AuditLogEntry>();
        var digitalInvoices = await db.DigitalInvoices
            .Where(item => item.CompanyId == companyId && item.InvoiceId == invoiceId && !item.Deleted)
            .ToListAsync(cancellationToken);

        // Every distinct Stock a removed movement belonged to needs its cached PurchaseQty/SoldQty/CostPrice
        // rollup rebuilt from the real (now-shorter) ledger once the rows are actually gone from the DB -
        // leaving these stale is exactly what made "insufficient stock" checks on a later reimport look at
        // the wrong number, since those checks replay real StockMovement rows, not the cached rollup, but
        // other stock-availability paths in this codebase do read the cached fields.
        var affectedStockIds = stockMovements
            .Where(item => item.StockId.HasValue)
            .Select(item => item.StockId!.Value)
            .Distinct()
            .ToList();

        var response = new AdminHardDeleteSaleResponse(
            invoiceId,
            invoiceNumber,
            "HardDeleted",
            invoiceItems.Count,
            invoicePayments.Count,
            cardPayments.Count,
            stockMovements.Count,
            journalEntries.Count,
            journalLines.Count,
            bankTransactions.Count,
            bankStatementLines.Count,
            chequeLogs.Count,
            commercialNotes.Count,
            loyaltyLedgers.Count,
            auditEntries.Count);

        db.BankStatementLines.RemoveRange(bankStatementLines);
        db.ChequeLogs.RemoveRange(chequeLogs);
        db.BankTransactions.RemoveRange(bankTransactions);
        db.JournalLines.RemoveRange(journalLines);
        db.JournalEntries.RemoveRange(journalEntries);
        db.InvoicePayments.RemoveRange(invoicePayments);
        db.CardPayments.RemoveRange(cardPayments);
        db.InvoiceItems.RemoveRange(invoiceItems);
        db.StockMovements.RemoveRange(stockMovements);
        db.CommercialNotes.RemoveRange(commercialNotes);
        db.LoyaltyPointLedgers.RemoveRange(loyaltyLedgers);
        db.SalesInvoices.Remove(invoice);
        if (auditEntries.Count > 0)
        {
            db.AuditLogEntries.RemoveRange(auditEntries);
        }
        foreach (var digitalInvoice in digitalInvoices)
        {
            // Soft-delete rather than remove: keeps WhatsApp log / feedback / campaign
            // recipient history intact while stopping the CRM Digital Bills list from
            // still showing a live-looking link for an invoice that no longer exists.
            digitalInvoice.Deleted = true;
            digitalInvoice.IsActive = false;
            digitalInvoice.DisabledAt = DateTime.UtcNow;
            digitalInvoice.DisableReason = "Source sale invoice was hard-deleted";
            digitalInvoice.UpdatedAt = DateTime.UtcNow;
        }

        // Flush the removals first - RebuildProjectionAsync re-queries StockMovements from the database,
        // so the rows have to be physically gone before it replays what's left.
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

        return response;
    }
}
