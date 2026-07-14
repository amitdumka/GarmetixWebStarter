using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Purchase;

public static class PurchaseReturnAdvancedSettlementEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 300;

    public static RouteGroupBuilder MapPurchaseReturnAdvancedSettlementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase-return/advanced-settlement")
            .WithTags("Purchase Return Advanced Settlement")
            .RequireAuthorization(GarmetixPolicies.Purchase);

        group.MapGet("", GetReportAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<PurchaseReturnAdvancedSettlementReportDto> GetReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildReportAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
    }

    private static async Task<IResult> ExportEvidenceCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
        var csv = BuildCsv(report);
        var fileName = $"garmetix-purchase-return-advanced-settlement-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<PurchaseReturnAdvancedSettlementReportDto> BuildReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate, toExclusive) = ResolveDateRange(from, to);

        var returnQuery = WorkspaceScope.ApplyTo(db.PurchaseReturns.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) returnQuery = returnQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) returnQuery = returnQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) returnQuery = returnQuery.Where(item => item.StoreId == storeId.Value);

        var returns = await returnQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var returnIds = returns.Select(item => item.Id).Distinct().ToArray();
        var debitNoteIds = returns.Where(item => item.DebitNoteId.HasValue).Select(item => item.DebitNoteId!.Value).Distinct().ToArray();

        var items = returnIds.Length == 0
            ? new List<PurchaseReturnItem>()
            : await db.PurchaseReturnItems.AsNoTracking()
                .Where(item => !item.Deleted && returnIds.Contains(item.PurchaseReturnId))
                .ToListAsync(cancellationToken);
        var reversals = returnIds.Length == 0
            ? new List<PurchaseReturnItcReversal>()
            : await db.PurchaseReturnItcReversals.AsNoTracking()
                .Where(item => !item.Deleted && returnIds.Contains(item.PurchaseReturnId))
                .ToListAsync(cancellationToken);
        var stockMovements = returnIds.Length == 0
            ? new List<StockMovement>()
            : await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "PurchaseReturn" && item.SourceId.HasValue && returnIds.Contains(item.SourceId.Value))
                .ToListAsync(cancellationToken);
        var debitNotes = debitNoteIds.Length == 0
            ? new List<CommercialNote>()
            : await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
                .Where(item => !item.Deleted && debitNoteIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var settlements = returnIds.Length == 0
            ? new List<VendorSettlement>()
            : await WorkspaceScope.ApplyTo(db.VendorSettlements.AsNoTracking(), context)
                .Where(item => !item.Deleted && returnIds.Contains(item.PurchaseReturnId))
                .ToListAsync(cancellationToken);
        var settlementIds = settlements.Select(item => item.Id).Distinct().ToArray();
        var settlementAllocations = settlementIds.Length == 0
            ? new List<VendorSettlementAllocation>()
            : await db.VendorSettlementAllocations.AsNoTracking()
                .Where(item => !item.Deleted && settlementIds.Contains(item.VendorSettlementId))
                .ToListAsync(cancellationToken);
        var settlementPayments = settlementIds.Length == 0
            ? new List<PurchasePayment>()
            : await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted
                    && item.AdjustmentSourceType == "VendorSettlement"
                    && item.AdjustmentSourceId.HasValue
                    && settlementIds.Contains(item.AdjustmentSourceId.Value))
                .ToListAsync(cancellationToken);
        var voucherIds = settlements.Where(item => item.VoucherId.HasValue).Select(item => item.VoucherId!.Value).Distinct().ToArray();
        var vouchers = voucherIds.Length == 0
            ? new List<Voucher>()
            : await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
                .Where(item => !item.Deleted && voucherIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var returnJournals = returnIds.Length == 0
            ? new List<JournalEntry>()
            : await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "PurchaseReturn" && item.SourceId.HasValue && returnIds.Contains(item.SourceId.Value))
                .ToListAsync(cancellationToken);
        var settlementJournals = settlementIds.Length == 0
            ? new List<JournalEntry>()
            : await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "VendorRefundSettlement" && item.SourceId.HasValue && settlementIds.Contains(item.SourceId.Value))
                .ToListAsync(cancellationToken);
        var journalIds = returnJournals.Concat(settlementJournals)
            .Select(item => item.Id)
            .Concat(returns.Where(item => item.JournalEntryId.HasValue).Select(item => item.JournalEntryId!.Value))
            .Concat(settlements.Where(item => item.JournalEntryId.HasValue).Select(item => item.JournalEntryId!.Value))
            .Distinct()
            .ToArray();
        var journalLines = journalIds.Length == 0
            ? new List<JournalLine>()
            : await WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context)
                .Where(item => !item.Deleted && journalIds.Contains(item.JournalEntryId))
                .ToListAsync(cancellationToken);

        var companyIds = returns.Select(item => item.CompanyId).Distinct().ToArray();
        var inputGstLedgerIds = companyIds.Length == 0
            ? new HashSet<Guid>()
            : (await db.Ledgers.AsNoTracking()
                .Where(item => companyIds.Contains(item.CompanyId) && item.Name == "Input GST")
                .Select(item => item.Id)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var itemsByReturn = items.GroupBy(item => item.PurchaseReturnId).ToDictionary(group => group.Key, group => group.ToList());
        var reversalsByReturn = reversals.GroupBy(item => item.PurchaseReturnId).ToDictionary(group => group.Key, group => group.ToList());
        var stockByReturn = stockMovements.GroupBy(item => item.SourceId!.Value).ToDictionary(group => group.Key, group => group.ToList());
        var debitNotesById = debitNotes.ToDictionary(item => item.Id);
        var settlementsByReturn = settlements.GroupBy(item => item.PurchaseReturnId).ToDictionary(group => group.Key, group => group.ToList());
        var allocationsBySettlement = settlementAllocations.GroupBy(item => item.VendorSettlementId).ToDictionary(group => group.Key, group => group.ToList());
        var paymentsBySettlement = settlementPayments.Where(item => item.AdjustmentSourceId.HasValue).GroupBy(item => item.AdjustmentSourceId!.Value).ToDictionary(group => group.Key, group => group.ToList());
        var vouchersById = vouchers.ToDictionary(item => item.Id);
        var returnJournalsByReturn = returnJournals.Where(item => item.SourceId.HasValue).GroupBy(item => item.SourceId!.Value).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.PostedAt).First());
        var settlementJournalsBySettlement = settlementJournals.Where(item => item.SourceId.HasValue).GroupBy(item => item.SourceId!.Value).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.PostedAt).First());
        var journalLinesByJournal = journalLines.GroupBy(item => item.JournalEntryId).ToDictionary(group => group.Key, group => group.ToList());

        var issues = new List<PurchaseReturnAdvancedSettlementIssueDto>();
        var evidenceRows = new List<PurchaseReturnAdvancedSettlementEvidenceRowDto>();

        foreach (var purchaseReturn in returns)
        {
            itemsByReturn.TryGetValue(purchaseReturn.Id, out var returnItems);
            returnItems ??= new List<PurchaseReturnItem>();
            reversalsByReturn.TryGetValue(purchaseReturn.Id, out var returnReversals);
            returnReversals ??= new List<PurchaseReturnItcReversal>();
            stockByReturn.TryGetValue(purchaseReturn.Id, out var movementRows);
            movementRows ??= new List<StockMovement>();
            var note = purchaseReturn.DebitNoteId.HasValue && debitNotesById.TryGetValue(purchaseReturn.DebitNoteId.Value, out var debitNote) ? debitNote : null;
            settlementsByReturn.TryGetValue(purchaseReturn.Id, out var settlementRows);
            settlementRows ??= new List<VendorSettlement>();

            var returnJournal = purchaseReturn.JournalEntryId.HasValue
                ? returnJournals.FirstOrDefault(item => item.Id == purchaseReturn.JournalEntryId.Value) ?? returnJournalsByReturn.GetValueOrDefault(purchaseReturn.Id)
                : returnJournalsByReturn.GetValueOrDefault(purchaseReturn.Id);
            var returnJournalLines = returnJournal is null
                ? new List<JournalLine>()
                : journalLinesByJournal.GetValueOrDefault(returnJournal.Id) ?? new List<JournalLine>();

            var itemQuantity = Round(returnItems.Sum(item => item.ReturnedQuantity));
            var itemTaxable = Round(returnItems.Sum(item => item.TaxableAmount));
            var itemTax = Round(returnItems.Sum(item => item.TaxAmount));
            var itemCgst = Round(returnItems.Sum(item => item.CGSTAmount));
            var itemSgst = Round(returnItems.Sum(item => item.SGSTAmount));
            var itemIgst = Round(returnItems.Sum(item => item.IGSTAmount));
            var itemAmount = Round(returnItems.Sum(item => item.ReturnAmount));
            var reversalTaxable = Round(returnReversals.Sum(item => item.TaxableAmount));
            var reversalTax = Round(returnReversals.Sum(item => item.TaxAmount));
            var reversalCgst = Round(returnReversals.Sum(item => item.CGSTAmount));
            var reversalSgst = Round(returnReversals.Sum(item => item.SGSTAmount));
            var reversalIgst = Round(returnReversals.Sum(item => item.IGSTAmount));
            var reversalComponentTax = Round(returnReversals.Sum(item => item.CGSTAmount + item.SGSTAmount + item.IGSTAmount));
            var stockOutQuantity = Round(movementRows.Sum(item => item.QuantityOut));
            var settlementAdjusted = Round(settlementRows.Sum(item => item.AdjustedAmount));
            var settlementRefund = Round(settlementRows.Sum(item => item.RefundAmount));
            var settlementTotal = Round(settlementRows.Sum(item => item.TotalAmount));
            var allocationTotal = Round(settlementRows.Sum(row => allocationsBySettlement.GetValueOrDefault(row.Id)?.Sum(item => item.Amount) ?? 0m));
            var settlementPaymentTotal = Round(settlementRows.Sum(row => paymentsBySettlement.GetValueOrDefault(row.Id)?.Sum(item => item.Amount) ?? 0m));
            var journalDebit = Round(returnJournalLines.Sum(item => item.Debit));
            var journalCredit = Round(returnJournalLines.Sum(item => item.Credit));
            var journalItcCredit = Round(returnJournalLines.Where(item => inputGstLedgerIds.Contains(item.LedgerId)).Sum(item => item.Credit));

            var missingDocument = string.IsNullOrWhiteSpace(purchaseReturn.ReturnNumber);
            var missingItems = returnItems.Count == 0;
            var itemCountMismatch = purchaseReturn.ItemCount != returnItems.Count;
            var quantityMismatch = Math.Abs(Round(purchaseReturn.Quantity) - itemQuantity) > AmountTolerance;
            var taxableMismatch = Math.Abs(Round(purchaseReturn.TaxableAmount) - itemTaxable) > AmountTolerance;
            var taxMismatch = Math.Abs(Round(purchaseReturn.TaxAmount) - itemTax) > AmountTolerance;
            var amountMismatch = Math.Abs(Round(purchaseReturn.ReturnAmount) - itemAmount) > AmountTolerance;
            var itcRowMismatch = returnItems.Count != returnReversals.Count;
            var itcTotalMismatch = Math.Abs(Round(purchaseReturn.TaxAmount) - reversalTax) > AmountTolerance;
            var itcComponentMismatch = Math.Abs(reversalTax - reversalComponentTax) > AmountTolerance
                || Math.Abs(Round(purchaseReturn.CGSTAmount) - reversalCgst) > AmountTolerance
                || Math.Abs(Round(purchaseReturn.SGSTAmount) - reversalSgst) > AmountTolerance
                || Math.Abs(Round(purchaseReturn.IGSTAmount) - reversalIgst) > AmountTolerance;
            var stockMismatch = Math.Abs(Round(purchaseReturn.Quantity) - stockOutQuantity) > AmountTolerance;
            var debitNoteMissing = note is null;
            var debitNoteAmountMismatch = note is not null && (Math.Abs(Round(note.Amount) - Round(purchaseReturn.ReturnAmount)) > AmountTolerance || Math.Abs(Round(note.TaxAmount) - Round(purchaseReturn.TaxAmount)) > AmountTolerance);
            var debitNoteSourceMismatch = note is not null && (!string.Equals(note.SourceType, "PurchaseReturn", StringComparison.OrdinalIgnoreCase) || note.SourceId != purchaseReturn.Id || note.NoteType != NoteType.DebitNote);
            var debitNoteOverAdjusted = note is not null && Round(note.AdjustedAmount) - Round(note.Amount) > AmountTolerance;
            var debitNoteAdjustedMismatch = note is not null && Math.Abs(Round(note.AdjustedAmount) - settlementTotal) > AmountTolerance;
            var returnSettlementMismatch = Math.Abs(Round(purchaseReturn.SettledAmount) - settlementTotal) > AmountTolerance;
            var settlementOverReturn = settlementTotal - Round(purchaseReturn.ReturnAmount) > AmountTolerance;
            var settlementPaymentMismatch = Math.Abs(settlementAdjusted - settlementPaymentTotal) > AmountTolerance;
            var allocationMismatch = Math.Abs(settlementAdjusted - allocationTotal) > AmountTolerance;
            var refundVoucherMissing = settlementRows.Count(item => item.RefundAmount > AmountTolerance && !item.VoucherId.HasValue) > 0;
            var refundVoucherInactive = settlementRows.Count(item => item.RefundAmount > AmountTolerance && item.VoucherId.HasValue && !vouchersById.ContainsKey(item.VoucherId.Value)) > 0;
            var refundNonCashMissingBank = settlementRows.Count(item => item.RefundAmount > AmountTolerance && RequiresBankMapping(item.PaymentMode) && !item.BankAccountId.HasValue) > 0;
            var refundNonCashMissingReference = settlementRows.Count(item => item.RefundAmount > AmountTolerance && RequiresReference(item.PaymentMode) && string.IsNullOrWhiteSpace(item.ReferenceNumber)) > 0;
            var refundJournalMissing = settlementRows.Count(item => item.RefundAmount > AmountTolerance && !HasSettlementJournal(item, settlementJournalsBySettlement)) > 0;
            var refundBankTransactionMissing = settlementRows.Count(item => item.RefundAmount > AmountTolerance && RequiresBankMapping(item.PaymentMode) && !item.BankTransactionId.HasValue) > 0;
            var journalMissing = returnJournal is null;
            var journalUnbalanced = returnJournal is not null && Math.Abs(journalDebit - journalCredit) > AmountTolerance;
            var journalItcMismatch = returnJournal is not null && Math.Abs(reversalTax - journalItcCredit) > AmountTolerance;
            var notPrinted = !purchaseReturn.Printed;
            var itcStatusNotPosted = !string.Equals(purchaseReturn.ItcReversalStatus, "Reconciled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(purchaseReturn.ItcReversalStatus, "Posted", StringComparison.OrdinalIgnoreCase);
            var settlementStatusMismatch = ExpectedSettlementStatus(purchaseReturn.ReturnAmount, settlementTotal) != purchaseReturn.SettlementStatus;

            AddIssueIf(issues, missingDocument, "Critical", "PURCHASE_RETURN_NUMBER_MISSING", purchaseReturn, "Formal purchase return document number is missing.", null);
            AddIssueIf(issues, missingItems, "Critical", "PURCHASE_RETURN_ITEMS_MISSING", purchaseReturn, "Purchase return has no item rows; supplier return cannot be audited.", null);
            AddIssueIf(issues, itemCountMismatch, "Critical", "PURCHASE_RETURN_ITEM_COUNT_MISMATCH", purchaseReturn, $"Header item count {purchaseReturn.ItemCount} differs from item rows {returnItems.Count}.", purchaseReturn.ItemCount - returnItems.Count);
            AddIssueIf(issues, quantityMismatch, "Critical", "PURCHASE_RETURN_QUANTITY_MISMATCH", purchaseReturn, $"Header quantity {Money(purchaseReturn.Quantity)} differs from returned item quantity {Money(itemQuantity)}.", purchaseReturn.Quantity - itemQuantity);
            AddIssueIf(issues, taxableMismatch, "Critical", "PURCHASE_RETURN_TAXABLE_MISMATCH", purchaseReturn, $"Header taxable amount {Money(purchaseReturn.TaxableAmount)} differs from item taxable amount {Money(itemTaxable)}.", purchaseReturn.TaxableAmount - itemTaxable);
            AddIssueIf(issues, taxMismatch, "Critical", "PURCHASE_RETURN_TAX_MISMATCH", purchaseReturn, $"Header GST {Money(purchaseReturn.TaxAmount)} differs from item GST {Money(itemTax)}.", purchaseReturn.TaxAmount - itemTax);
            AddIssueIf(issues, amountMismatch, "Critical", "PURCHASE_RETURN_TOTAL_MISMATCH", purchaseReturn, $"Header return amount {Money(purchaseReturn.ReturnAmount)} differs from item return amount {Money(itemAmount)}.", purchaseReturn.ReturnAmount - itemAmount);
            AddIssueIf(issues, itcRowMismatch, "Critical", "ITC_REVERSAL_ROW_MISMATCH", purchaseReturn, $"ITC reversal rows {returnReversals.Count} do not match return item rows {returnItems.Count}.", returnReversals.Count - returnItems.Count);
            AddIssueIf(issues, itcTotalMismatch, "Critical", "ITC_REVERSAL_TOTAL_MISMATCH", purchaseReturn, $"ITC reversal GST {Money(reversalTax)} differs from return GST {Money(purchaseReturn.TaxAmount)}.", reversalTax - purchaseReturn.TaxAmount);
            AddIssueIf(issues, itcComponentMismatch, "Critical", "ITC_REVERSAL_COMPONENT_MISMATCH", purchaseReturn, "CGST/SGST/IGST component reversal does not exactly match the purchase return tax split.", reversalComponentTax - purchaseReturn.TaxAmount);
            AddIssueIf(issues, stockMismatch, "Critical", "PURCHASE_RETURN_STOCK_MISMATCH", purchaseReturn, $"Stock-out quantity {Money(stockOutQuantity)} differs from returned quantity {Money(purchaseReturn.Quantity)}.", stockOutQuantity - purchaseReturn.Quantity);
            AddIssueIf(issues, debitNoteMissing, "Critical", "VENDOR_DEBIT_NOTE_MISSING", purchaseReturn, "Purchase return has no linked supplier debit note.", purchaseReturn.ReturnAmount);
            AddIssueIf(issues, debitNoteAmountMismatch, "Critical", "VENDOR_DEBIT_NOTE_AMOUNT_MISMATCH", purchaseReturn, "Linked vendor debit note amount/tax does not match purchase return amount/tax.", note is null ? null : note.Amount - purchaseReturn.ReturnAmount);
            AddIssueIf(issues, debitNoteSourceMismatch, "Critical", "VENDOR_DEBIT_NOTE_SOURCE_MISMATCH", purchaseReturn, "Linked debit note is not clearly sourced from this PurchaseReturn document.", null);
            AddIssueIf(issues, debitNoteOverAdjusted, "Critical", "VENDOR_DEBIT_NOTE_OVERADJUSTED", purchaseReturn, "Linked debit note is adjusted above its note amount.", note is null ? null : note.AdjustedAmount - note.Amount);
            AddIssueIf(issues, settlementOverReturn, "Critical", "PURCHASE_RETURN_OVER_SETTLED", purchaseReturn, "Vendor settlement total exceeds purchase return amount.", settlementTotal - purchaseReturn.ReturnAmount);
            AddIssueIf(issues, settlementPaymentMismatch, "Critical", "DEBIT_NOTE_SETTLEMENT_PAYMENT_MISMATCH", purchaseReturn, "Debit-note adjustment payment rows do not equal settlement adjusted amount.", settlementAdjusted - settlementPaymentTotal);
            AddIssueIf(issues, journalMissing, "Critical", "PURCHASE_RETURN_JOURNAL_MISSING", purchaseReturn, "Purchase return accounting journal is missing.", null);
            AddIssueIf(issues, journalUnbalanced, "Critical", "PURCHASE_RETURN_JOURNAL_UNBALANCED", purchaseReturn, "Purchase return journal debit and credit totals are not equal.", journalDebit - journalCredit);
            AddIssueIf(issues, journalItcMismatch, "Critical", "PURCHASE_RETURN_JOURNAL_ITC_MISMATCH", purchaseReturn, "Input GST credit in purchase return journal does not equal ITC reversal amount.", journalItcCredit - reversalTax);
            AddIssueIf(issues, refundNonCashMissingBank, "Critical", "VENDOR_REFUND_BANK_MAPPING_MISSING", purchaseReturn, "One or more supplier refund settlements are non-cash but have no bank account mapping.", settlementRefund);
            AddIssueIf(issues, refundNonCashMissingReference, "Critical", "VENDOR_REFUND_REFERENCE_MISSING", purchaseReturn, "One or more supplier refund settlements are non-cash but have no UTR/cheque/reference number.", settlementRefund);
            AddIssueIf(issues, refundBankTransactionMissing, "Warning", "VENDOR_REFUND_BANK_TRANSACTION_MISSING", purchaseReturn, "One or more non-cash supplier refund settlements have no linked bank transaction evidence.", settlementRefund);
            AddIssueIf(issues, refundJournalMissing, "Warning", "VENDOR_REFUND_JOURNAL_MISSING", purchaseReturn, "One or more supplier refund settlements have no VendorRefundSettlement journal evidence.", settlementRefund);
            AddIssueIf(issues, refundVoucherMissing, "Warning", "VENDOR_REFUND_VOUCHER_MISSING", purchaseReturn, "One or more supplier refund settlements have no receipt voucher evidence.", settlementRefund);
            AddIssueIf(issues, refundVoucherInactive, "Warning", "VENDOR_REFUND_VOUCHER_INACTIVE", purchaseReturn, "One or more supplier refund settlements point to missing/deleted voucher evidence.", settlementRefund);
            AddIssueIf(issues, debitNoteAdjustedMismatch, "Warning", "DEBIT_NOTE_ADJUSTED_SETTLEMENT_MISMATCH", purchaseReturn, "Debit note adjusted amount does not match vendor settlement total.", note is null ? null : note.AdjustedAmount - settlementTotal);
            AddIssueIf(issues, returnSettlementMismatch, "Warning", "PURCHASE_RETURN_SETTLED_AMOUNT_MISMATCH", purchaseReturn, "Purchase return settled amount does not match vendor settlement total.", purchaseReturn.SettledAmount - settlementTotal);
            AddIssueIf(issues, allocationMismatch, "Warning", "VENDOR_SETTLEMENT_ALLOCATION_MISMATCH", purchaseReturn, "Vendor settlement invoice allocations do not equal settlement adjusted amount.", allocationTotal - settlementAdjusted);
            AddIssueIf(issues, notPrinted, "Warning", "PURCHASE_RETURN_NOT_PRINTED", purchaseReturn, "Formal purchase return document has not been printed/shared with supplier.", null);
            AddIssueIf(issues, itcStatusNotPosted, "Warning", "ITC_REVERSAL_STATUS_PENDING", purchaseReturn, "Purchase return ITC reversal status is still Pending/Needs Review.", reversalTax);
            AddIssueIf(issues, settlementStatusMismatch, "Warning", "PURCHASE_RETURN_SETTLEMENT_STATUS_MISMATCH", purchaseReturn, "Purchase return settlement status does not match settlement evidence.", settlementTotal);

            var status = BuildStatus(
                missingDocument,
                missingItems,
                itemCountMismatch,
                quantityMismatch,
                taxableMismatch,
                taxMismatch,
                amountMismatch,
                itcRowMismatch,
                itcTotalMismatch,
                itcComponentMismatch,
                stockMismatch,
                debitNoteMissing,
                debitNoteAmountMismatch,
                debitNoteSourceMismatch,
                debitNoteOverAdjusted,
                settlementOverReturn,
                settlementPaymentMismatch,
                journalMissing,
                journalUnbalanced,
                journalItcMismatch,
                refundNonCashMissingBank,
                refundNonCashMissingReference,
                refundBankTransactionMissing,
                refundJournalMissing,
                refundVoucherMissing,
                refundVoucherInactive,
                debitNoteAdjustedMismatch,
                returnSettlementMismatch,
                allocationMismatch,
                notPrinted,
                itcStatusNotPosted,
                settlementStatusMismatch);

            evidenceRows.Add(new PurchaseReturnAdvancedSettlementEvidenceRowDto(
                purchaseReturn.Id,
                purchaseReturn.ReturnNumber,
                purchaseReturn.OnDate,
                purchaseReturn.ReturnKind,
                purchaseReturn.Status,
                purchaseReturn.PurchaseInvoiceId,
                purchaseReturn.OriginalInvoiceNumber,
                purchaseReturn.OriginalInvoiceDate,
                purchaseReturn.VendorId,
                purchaseReturn.VendorName,
                purchaseReturn.VendorGstin,
                purchaseReturn.Quantity,
                itemQuantity,
                purchaseReturn.TaxableAmount,
                itemTaxable,
                purchaseReturn.TaxAmount,
                itemTax,
                reversalTaxable,
                reversalTax,
                journalItcCredit,
                purchaseReturn.CGSTAmount,
                purchaseReturn.SGSTAmount,
                purchaseReturn.IGSTAmount,
                reversalCgst,
                reversalSgst,
                reversalIgst,
                purchaseReturn.ReturnAmount,
                itemAmount,
                purchaseReturn.DebitNoteId,
                purchaseReturn.DebitNoteNumber,
                note?.Amount ?? 0m,
                note?.AdjustedAmount ?? 0m,
                note is null ? 0m : Math.Max(Round(note.Amount - note.AdjustedAmount), 0m),
                purchaseReturn.Printed,
                purchaseReturn.PrintCount,
                purchaseReturn.SettledAmount,
                settlementTotal,
                settlementAdjusted,
                settlementRefund,
                allocationTotal,
                settlementPaymentTotal,
                purchaseReturn.SettlementStatus,
                purchaseReturn.ItcReversalAmount,
                purchaseReturn.ItcReversalStatus,
                returnItems.Count,
                returnReversals.Count,
                stockOutQuantity,
                settlementRows.Count,
                returnJournal?.Id,
                returnJournal?.EntryNumber,
                journalDebit,
                journalCredit,
                status));
        }

        var critical = issues.Count(item => item.Severity == "Critical");
        var warnings = issues.Count(item => item.Severity == "Warning");
        var statusText = critical == 0 && warnings == 0 ? "Complete" : "Not Complete";
        var periodRows = evidenceRows.Where(item => item.OnDate >= fromDate && item.OnDate <= toDate).ToList();

        var metrics = new List<PurchaseReturnAdvancedSettlementMetricDto>
        {
            new("Purchase returns", returns.Count, Round(returns.Sum(item => item.ReturnAmount)), "Formal purchase-return documents in the selected period."),
            new("Returned quantity", returns.Count, Round(returns.Sum(item => item.Quantity)), "Total quantity returned to suppliers."),
            new("ITC reversal", reversals.Count, Round(reversals.Sum(item => item.TaxAmount)), "Exact CGST/SGST/IGST reversal rows linked to purchase returns."),
            new("Debit notes", debitNotes.Count, Round(debitNotes.Sum(item => item.Amount)), "Supplier debit-note documents created from purchase returns."),
            new("Settled amount", settlements.Count, Round(settlements.Sum(item => item.TotalAmount)), "Debit-note adjustment plus supplier refund settlement total."),
            new("Supplier refund", settlements.Count(item => item.RefundAmount > 0), Round(settlements.Sum(item => item.RefundAmount)), "Cash/bank refunds received from suppliers against debit notes."),
            new("Open settlement", evidenceRows.Count(item => item.DebitNoteOpenAmount > AmountTolerance), Round(evidenceRows.Sum(item => item.DebitNoteOpenAmount)), "Open debit-note value still pending settlement."),
            new("Accounting journals", returnJournals.Count, Round(returnJournals.Count), "PurchaseReturn journals found for ITC/accounting audit.")
        };

        return new PurchaseReturnAdvancedSettlementReportDto(
            statusText,
            fromDate,
            toDate,
            companyId,
            storeGroupId,
            storeId,
            critical,
            warnings,
            Round(returns.Sum(item => item.ReturnAmount)),
            Round(returns.Sum(item => item.TaxAmount)),
            Round(settlements.Sum(item => item.TotalAmount)),
            Round(settlements.Sum(item => item.RefundAmount)),
            Round(evidenceRows.Sum(item => item.DebitNoteOpenAmount)),
            metrics,
            issues
                .OrderBy(item => item.Severity == "Critical" ? 0 : 1)
                .ThenBy(item => item.ReturnNumber)
                .ThenBy(item => item.Code)
                .Take(EvidenceLimit)
                .ToList(),
            evidenceRows
                .OrderByDescending(item => item.OnDate)
                .ThenBy(item => item.ReturnNumber)
                .Take(EvidenceLimit)
                .ToList(),
            CloseoutChecklist(statusText),
            OperatorRules(),
            KnownLimitations(),
            NextModuleCandidates());
    }

    private static bool HasSettlementJournal(VendorSettlement settlement, IReadOnlyDictionary<Guid, JournalEntry> settlementJournalsBySettlement)
    {
        return settlement.JournalEntryId.HasValue || settlementJournalsBySettlement.ContainsKey(settlement.Id);
    }

    private static bool RequiresBankMapping(PaymentMode? mode)
    {
        return mode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;
    }

    private static bool RequiresReference(PaymentMode? mode)
    {
        return mode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;
    }

    private static string ExpectedSettlementStatus(decimal total, decimal settled)
    {
        var totalRounded = Round(total);
        var settledRounded = Round(settled);
        if (settledRounded <= AmountTolerance) return "Open";
        return settledRounded + AmountTolerance >= totalRounded ? "Settled" : "Partially Settled";
    }

    private static string BuildStatus(params bool[] checks)
    {
        var criticalChecks = checks.Take(21).Any(item => item);
        var warningChecks = checks.Skip(21).Any(item => item);
        if (criticalChecks) return "Critical";
        return warningChecks ? "Warning" : "Pass";
    }

    private static void AddIssueIf(List<PurchaseReturnAdvancedSettlementIssueDto> issues, bool condition, string severity, string code, PurchaseReturn purchaseReturn, string message, decimal? amount)
    {
        if (!condition) return;
        issues.Add(new PurchaseReturnAdvancedSettlementIssueDto(
            severity,
            code,
            purchaseReturn.Id,
            purchaseReturn.ReturnNumber,
            purchaseReturn.OnDate,
            purchaseReturn.VendorId,
            purchaseReturn.VendorName,
            purchaseReturn.VendorGstin,
            message,
            amount));
    }

    private static (DateTime From, DateTime To, DateTime ToExclusive) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toDate = (to ?? today).Date;
        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        return (fromDate, toDate, toDate.AddDays(1));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => $"₹{Round(value):N2}";

    private static IReadOnlyList<string> CloseoutChecklist(string status)
    {
        var lines = new List<string>
        {
            "Create every supplier return through the formal Purchase Return workflow, not by deleting purchase inward lines.",
            "Verify returned quantity, stock-out movement and debit-note amount against the supplier return proof.",
            "Confirm exact ITC reversal: taxable amount, CGST, SGST, IGST and total GST must match item snapshots.",
            "Print/share the purchase return and linked vendor debit note with the supplier.",
            "Settle every debit note through Vendor Settlements: adjustment against payable invoice, supplier refund, or both.",
            "For supplier refunds, capture payment mode, bank mapping and UTR/cheque/reference number before closeout.",
            "Export CSV evidence and attach it with Vendor Payable, GST/Accounting and FY Closeout evidence."
        };

        if (status != "Complete")
        {
            lines.Insert(0, "Status is Not Complete. Do not lock purchase/GST period until critical purchase-return issues are corrected.");
        }

        return lines;
    }

    private static IReadOnlyList<string> OperatorRules() => new[]
    {
        "Never directly delete a posted purchase inward to handle supplier returns; use Purchase Return so stock, debit note and ITC reversal remain auditable.",
        "Supplier refunds are receipts against vendor debit notes, not normal customer receipts and not manual cash income.",
        "Debit-note adjustment against future supplier bills must create visible VendorSettlement allocation and PurchasePayment evidence with PaymentMode DebitNote.",
        "Exact ITC reversal must follow original purchase tax snapshot and cannot be approximated manually at month close.",
        "Wrong return quantities should be corrected through a controlled reversal/re-entry process after backup, not direct table edits."
    };

    private static IReadOnlyList<string> KnownLimitations() => new[]
    {
        "This is an acceptance and evidence layer; it does not auto-correct posted purchase returns.",
        "Legacy returns created before formal PurchaseReturn/ITC reversal tables may need a controlled repair or re-entry plan.",
        "Supplier refund bank reconciliation depends on the bank transaction/statement import workflow being used consistently.",
        "The report uses stored purchase return tax snapshots; if original purchase tax was wrong, correct the original document through the approved revision process first."
    };

    private static IReadOnlyList<string> NextModuleCandidates() => new[]
    {
        "Production host build QA and runtime error fix pack",
        "Automated regression test suite for sale, purchase, returns, GST and closeout pages",
        "Purchase import supplier-specific OCR provider tuning",
        "Biometric attendance device production integration"
    };

    private static string BuildCsv(PurchaseReturnAdvancedSettlementReportDto report)
    {
        var sb = new StringBuilder();
        void Row(params object?[] values) => sb.AppendLine(string.Join(',', values.Select(Csv)));

        Row("Garmetix Purchase Return Advanced Settlement Acceptance");
        Row("Status", report.Status, "From", report.From.ToString("yyyy-MM-dd"), "To", report.To.ToString("yyyy-MM-dd"));
        Row("Critical", report.CriticalIssues, "Warnings", report.WarningIssues, "Return Amount", report.TotalReturnAmount, "ITC Reversal", report.TotalItcReversalAmount, "Settled", report.TotalSettledAmount, "Supplier Refund", report.TotalRefundAmount, "Open Debit Notes", report.TotalOpenDebitNoteAmount);
        Row();
        Row("Metrics");
        Row("Label", "Count", "Amount", "Description");
        foreach (var metric in report.Metrics) Row(metric.Label, metric.Count, metric.Amount, metric.Description);

        Row();
        Row("Issues");
        Row("Severity", "Code", "Return Number", "Date", "Vendor", "GSTIN", "Message", "Amount");
        foreach (var issue in report.Issues) Row(issue.Severity, issue.Code, issue.ReturnNumber, issue.OnDate, issue.VendorName, issue.VendorGstin, issue.Message, issue.Amount);

        Row();
        Row("Return Evidence");
        Row("Return Number", "Date", "Kind", "Status", "Purchase Invoice", "Vendor", "Quantity Header", "Quantity Items", "Taxable Header", "Taxable Items", "GST Header", "GST Items", "ITC Reversal", "Journal ITC Credit", "CGST Header", "SGST Header", "IGST Header", "CGST Reversed", "SGST Reversed", "IGST Reversed", "Return Amount", "Item Amount", "Debit Note", "Debit Note Amount", "Debit Adjusted", "Debit Open", "Printed", "Print Count", "Settled Header", "Settlement Total", "Adjusted", "Refund", "Allocated", "Debit Note Payment Rows", "Settlement Status", "ITC Status", "Items", "Reversals", "Stock Out", "Settlements", "Journal", "Journal Debit", "Journal Credit", "Status");
        foreach (var row in report.EvidenceRows)
        {
            Row(row.ReturnNumber, row.OnDate, row.ReturnKind, row.Status, row.OriginalInvoiceNumber, row.VendorName, row.HeaderQuantity, row.ItemQuantity, row.HeaderTaxableAmount, row.ItemTaxableAmount, row.HeaderTaxAmount, row.ItemTaxAmount, row.ItcReversalAmount, row.JournalItcCreditAmount, row.HeaderCgstAmount, row.HeaderSgstAmount, row.HeaderIgstAmount, row.ReversalCgstAmount, row.ReversalSgstAmount, row.ReversalIgstAmount, row.HeaderReturnAmount, row.ItemReturnAmount, row.DebitNoteNumber, row.DebitNoteAmount, row.DebitNoteAdjustedAmount, row.DebitNoteOpenAmount, row.Printed, row.PrintCount, row.HeaderSettledAmount, row.SettlementTotalAmount, row.SettlementAdjustedAmount, row.SettlementRefundAmount, row.SettlementAllocationAmount, row.SettlementPaymentRowsAmount, row.SettlementStatus, row.ItcReversalStatus, row.ItemRowCount, row.ItcReversalRowCount, row.StockOutQuantity, row.SettlementCount, row.JournalEntryNumber, row.JournalDebitAmount, row.JournalCreditAmount, row.EvidenceStatus);
        }

        Row();
        Row("Closeout Checklist");
        foreach (var item in report.CloseoutChecklist) Row(item);

        return sb.ToString();
    }

    private static string Csv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd"),
            decimal amount => amount.ToString("0.00"),
            bool flag => flag ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty
        };
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}

public record PurchaseReturnAdvancedSettlementReportDto(
    string Status,
    DateTime From,
    DateTime To,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    int CriticalIssues,
    int WarningIssues,
    decimal TotalReturnAmount,
    decimal TotalItcReversalAmount,
    decimal TotalSettledAmount,
    decimal TotalRefundAmount,
    decimal TotalOpenDebitNoteAmount,
    IReadOnlyList<PurchaseReturnAdvancedSettlementMetricDto> Metrics,
    IReadOnlyList<PurchaseReturnAdvancedSettlementIssueDto> Issues,
    IReadOnlyList<PurchaseReturnAdvancedSettlementEvidenceRowDto> EvidenceRows,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public record PurchaseReturnAdvancedSettlementMetricDto(string Label, int Count, decimal? Amount, string Description);

public record PurchaseReturnAdvancedSettlementIssueDto(
    string Severity,
    string Code,
    Guid PurchaseReturnId,
    string ReturnNumber,
    DateTime OnDate,
    Guid VendorId,
    string VendorName,
    string? VendorGstin,
    string Message,
    decimal? Amount);

public record PurchaseReturnAdvancedSettlementEvidenceRowDto(
    Guid PurchaseReturnId,
    string ReturnNumber,
    DateTime OnDate,
    string ReturnKind,
    string Status,
    Guid PurchaseInvoiceId,
    string OriginalInvoiceNumber,
    DateTime OriginalInvoiceDate,
    Guid VendorId,
    string VendorName,
    string? VendorGstin,
    decimal HeaderQuantity,
    decimal ItemQuantity,
    decimal HeaderTaxableAmount,
    decimal ItemTaxableAmount,
    decimal HeaderTaxAmount,
    decimal ItemTaxAmount,
    decimal ItcTaxableAmount,
    decimal ItcReversalAmount,
    decimal JournalItcCreditAmount,
    decimal HeaderCgstAmount,
    decimal HeaderSgstAmount,
    decimal HeaderIgstAmount,
    decimal ReversalCgstAmount,
    decimal ReversalSgstAmount,
    decimal ReversalIgstAmount,
    decimal HeaderReturnAmount,
    decimal ItemReturnAmount,
    Guid? DebitNoteId,
    string? DebitNoteNumber,
    decimal DebitNoteAmount,
    decimal DebitNoteAdjustedAmount,
    decimal DebitNoteOpenAmount,
    bool Printed,
    int PrintCount,
    decimal HeaderSettledAmount,
    decimal SettlementTotalAmount,
    decimal SettlementAdjustedAmount,
    decimal SettlementRefundAmount,
    decimal SettlementAllocationAmount,
    decimal SettlementPaymentRowsAmount,
    string SettlementStatus,
    decimal HeaderItcReversalAmount,
    string ItcReversalStatus,
    int ItemRowCount,
    int ItcReversalRowCount,
    decimal StockOutQuantity,
    int SettlementCount,
    Guid? JournalEntryId,
    string? JournalEntryNumber,
    decimal JournalDebitAmount,
    decimal JournalCreditAmount,
    string EvidenceStatus);
