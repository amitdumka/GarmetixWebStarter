using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Purchase;

public static class VendorPayableReconciliationEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 250;

    public static RouteGroupBuilder MapVendorPayableReconciliationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase/vendor-payable-reconciliation")
            .WithTags("Vendor Payable Reconciliation")
            .RequireAuthorization(GarmetixPolicies.Purchase);

        group.MapGet("", GetReconciliationAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<VendorPayableReconciliationReportDto> GetReconciliationAsync(
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
        var fileName = $"garmetix-vendor-payable-reconciliation-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<VendorPayableReconciliationReportDto> BuildReportAsync(
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

        var invoiceQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.InwardDate < toExclusive && item.InvoiceStatus != InvoiceStatus.Cancelled && item.InvoiceStatus != InvoiceStatus.Refunded && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return);
        if (companyId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) invoiceQuery = invoiceQuery.Where(item => item.StoreId == storeId.Value);
        var invoices = await invoiceQuery.ToListAsync(cancellationToken);

        var paymentQuery = WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate < toExclusive);
        if (companyId.HasValue) paymentQuery = paymentQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) paymentQuery = paymentQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) paymentQuery = paymentQuery.Where(item => item.StoreId == storeId.Value);
        var payments = await paymentQuery.ToListAsync(cancellationToken);

        var noteQuery = WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate < toExclusive && item.NoteType == NoteType.DebitNote && item.VendorId.HasValue);
        if (companyId.HasValue) noteQuery = noteQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) noteQuery = noteQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) noteQuery = noteQuery.Where(item => item.StoreId == storeId.Value);
        var debitNotes = await noteQuery.ToListAsync(cancellationToken);

        var settlementQuery = WorkspaceScope.ApplyTo(db.VendorSettlements.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate < toExclusive);
        if (companyId.HasValue) settlementQuery = settlementQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) settlementQuery = settlementQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) settlementQuery = settlementQuery.Where(item => item.StoreId == storeId.Value);
        var settlements = await settlementQuery.ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).Distinct().ToArray();
        var paymentVoucherIds = payments.Where(item => item.VoucherId.HasValue).Select(item => item.VoucherId!.Value).Distinct().ToArray();

        var itemCounts = invoiceIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await db.PurchaseInvoiceItems.AsNoTracking()
                .Where(item => !item.Deleted && invoiceIds.Contains(item.InvoiceId))
                .GroupBy(item => item.InvoiceId)
                .Select(group => new { InvoiceId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.InvoiceId, item => item.Count, cancellationToken);

        var vouchers = paymentVoucherIds.Length == 0
            ? new Dictionary<Guid, Voucher>()
            : await WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
                .Where(item => paymentVoucherIds.Contains(item.Id) && !item.Deleted)
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var purchaseJournalIds = invoiceIds.Length == 0
            ? new HashSet<Guid>()
            : (await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "PurchaseInvoice" && item.SourceId.HasValue && invoiceIds.Contains(item.SourceId.Value))
                .Select(item => item.SourceId!.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var voucherJournalIds = paymentVoucherIds.Length == 0
            ? new HashSet<Guid>()
            : (await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceType == "VendorPaymentVoucher" && item.SourceId.HasValue && paymentVoucherIds.Contains(item.SourceId.Value))
                .Select(item => item.SourceId!.Value)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var relevantVendorIds = new HashSet<Guid>();
        foreach (var id in invoices.Select(item => item.VendorId)) relevantVendorIds.Add(id);
        foreach (var id in payments.Select(item => item.VendorId)) relevantVendorIds.Add(id);
        foreach (var id in debitNotes.Where(item => item.VendorId.HasValue).Select(item => item.VendorId!.Value)) relevantVendorIds.Add(id);
        foreach (var id in settlements.Select(item => item.VendorId)) relevantVendorIds.Add(id);

        var vendors = relevantVendorIds.Count == 0
            ? new List<Vendor>()
            : await WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context)
                .Where(item => relevantVendorIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var invoicesByVendor = invoices.GroupBy(item => item.VendorId).ToDictionary(group => group.Key, group => group.ToList());
        var paymentsByVendor = payments.GroupBy(item => item.VendorId).ToDictionary(group => group.Key, group => group.ToList());
        var debitNotesByVendor = debitNotes.Where(item => item.VendorId.HasValue).GroupBy(item => item.VendorId!.Value).ToDictionary(group => group.Key, group => group.ToList());
        var settlementsByVendor = settlements.GroupBy(item => item.VendorId).ToDictionary(group => group.Key, group => group.ToList());
        var paymentsByInvoice = payments.Where(item => item.PurchaseInvoiceId != Guid.Empty).GroupBy(item => item.PurchaseInvoiceId).ToDictionary(group => group.Key, group => group.ToList());

        var issues = new List<VendorPayableIssueDto>();
        var evidenceRows = new List<VendorPayableEvidenceRowDto>();

        foreach (var vendor in vendors.OrderBy(item => item.Name))
        {
            var vendorInvoices = invoicesByVendor.TryGetValue(vendor.Id, out var invoiceRows) ? invoiceRows : new List<PurchaseInvoice>();
            var vendorPayments = paymentsByVendor.TryGetValue(vendor.Id, out var paymentRows) ? paymentRows : new List<PurchasePayment>();
            var vendorDebitNotes = debitNotesByVendor.TryGetValue(vendor.Id, out var noteRows) ? noteRows : new List<CommercialNote>();
            var vendorSettlements = settlementsByVendor.TryGetValue(vendor.Id, out var settlementRows) ? settlementRows : new List<VendorSettlement>();

            var invoiceTotal = Round(vendorInvoices.Sum(item => item.BillAmount));
            var invoicePaid = Round(vendorInvoices.Sum(invoice => paymentsByInvoice.TryGetValue(invoice.Id, out var rows) ? rows.Sum(row => row.Amount) : 0m));
            var invoiceOutstanding = Round(vendorInvoices.Sum(invoice => Math.Max(invoice.BillAmount - (paymentsByInvoice.TryGetValue(invoice.Id, out var rows) ? rows.Sum(row => row.Amount) : 0m), 0m)));
            var activePaymentTotal = Round(vendorPayments.Sum(item => item.Amount));
            var advancePaymentTotal = Round(vendorPayments.Where(IsVendorAdvance).Sum(item => item.Amount));
            var debitNoteSettlementPayment = Round(vendorPayments.Where(IsDebitNoteSettlementPayment).Sum(item => item.Amount));
            var openDebitNoteAmount = Round(vendorDebitNotes.Sum(item => Math.Max(item.Amount - item.AdjustedAmount, 0m)));
            var payableAfterOpenDebitNotes = Round(Math.Max(invoiceOutstanding - openDebitNoteAmount, 0m));
            var masterBalance = Round(vendor.BillAmount - vendor.Paid);
            var expectedBalance = Round(invoiceTotal - activePaymentTotal - openDebitNoteAmount);
            var masterBillDifference = Round(vendor.BillAmount - invoiceTotal);
            var masterPaidDifference = Round(vendor.Paid - activePaymentTotal);
            var balanceDifference = Round(masterBalance - expectedBalance);

            var invoiceStatusMismatch = 0;
            var paidStatusWithBalance = 0;
            var overpaidInvoices = 0;
            var missingItemRows = 0;
            var purchaseJournalMissing = 0;
            foreach (var invoice in vendorInvoices)
            {
                paymentsByInvoice.TryGetValue(invoice.Id, out var rows);
                var paid = Round(rows?.Sum(item => item.Amount) ?? 0m);
                var balance = Round(invoice.BillAmount - paid);
                if (balance < -AmountTolerance) overpaidInvoices++;
                if (invoice.InvoiceStatus == InvoiceStatus.Paid && balance > AmountTolerance) paidStatusWithBalance++;
                if (invoice.InvoiceStatus != InvoiceStatus.PartiallyRefunded)
                {
                    var expected = ExpectedInvoiceStatus(invoice.BillAmount, paid);
                    if (invoice.InvoiceStatus != expected) invoiceStatusMismatch++;
                }
                if (!itemCounts.TryGetValue(invoice.Id, out var count) || count == 0) missingItemRows++;
                if (!purchaseJournalIds.Contains(invoice.Id)) purchaseJournalMissing++;
            }

            var nonCashMissingBank = vendorPayments.Count(item => RequiresBankMapping(item.PaymentMode) && !item.BankAccountId.HasValue);
            var voucherMissing = vendorPayments.Count(item => ShouldHaveVoucher(item) && !item.VoucherId.HasValue);
            var inactiveVoucher = vendorPayments.Count(item => item.VoucherId.HasValue && !vouchers.ContainsKey(item.VoucherId.Value));
            var voucherAmountMismatch = vendorPayments.Count(item => item.VoucherId.HasValue && vouchers.TryGetValue(item.VoucherId.Value, out var voucher) && Math.Abs(voucher.Amount - item.Amount) > AmountTolerance);
            var voucherModeMismatch = vendorPayments.Count(item => item.VoucherId.HasValue && vouchers.TryGetValue(item.VoucherId.Value, out var voucher) && voucher.PaymentMode != item.PaymentMode);
            var voucherJournalMissing = vendorPayments.Count(item => item.VoucherId.HasValue && vouchers.ContainsKey(item.VoucherId.Value) && !voucherJournalIds.Contains(item.VoucherId.Value));
            var debitNoteOverAdjusted = vendorDebitNotes.Count(item => item.AdjustedAmount - item.Amount > AmountTolerance);
            var debitNoteUnprintedOpen = vendorDebitNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance && !item.Printed);
            var debitNoteSettlementMismatch = vendorDebitNotes.Count(note =>
            {
                var settlementAmount = vendorSettlements.Where(item => item.DebitNoteId == note.Id).Sum(item => item.TotalAmount);
                return Math.Abs(Round(settlementAmount) - Round(note.AdjustedAmount)) > AmountTolerance;
            });

            AddIssueIf(issues, Math.Abs(masterBillDifference) > AmountTolerance, "Critical", "VENDOR_MASTER_BILL_MISMATCH", vendor, $"Vendor master bill amount {Money(vendor.BillAmount)} differs from active purchase invoice total {Money(invoiceTotal)}.", masterBillDifference);
            AddIssueIf(issues, Math.Abs(masterPaidDifference) > AmountTolerance, "Critical", "VENDOR_MASTER_PAID_MISMATCH", vendor, $"Vendor master paid amount {Money(vendor.Paid)} differs from active purchase payment rows {Money(activePaymentTotal)}.", masterPaidDifference);
            AddIssueIf(issues, Math.Abs(balanceDifference) > AmountTolerance, "Critical", "VENDOR_BALANCE_MISMATCH", vendor, $"Vendor master balance {Money(masterBalance)} differs from invoice outstanding minus open debit notes {Money(expectedBalance)}.", balanceDifference);
            AddIssueIf(issues, invoiceStatusMismatch > 0, "Critical", "PURCHASE_INVOICE_STATUS_MISMATCH", vendor, $"{invoiceStatusMismatch} purchase invoice(s) have status different from payment-row evidence.", invoiceStatusMismatch);
            AddIssueIf(issues, paidStatusWithBalance > 0, "Critical", "PURCHASE_PAID_STATUS_WITH_BALANCE", vendor, $"{paidStatusWithBalance} purchase invoice(s) are marked Paid while balance remains.", paidStatusWithBalance);
            AddIssueIf(issues, overpaidInvoices > 0, "Critical", "PURCHASE_OVERPAID_INVOICE", vendor, $"{overpaidInvoices} purchase invoice(s) are overpaid against bill amount.", overpaidInvoices);
            AddIssueIf(issues, debitNoteOverAdjusted > 0, "Critical", "VENDOR_DEBIT_NOTE_OVERADJUSTED", vendor, $"{debitNoteOverAdjusted} vendor debit note(s) are adjusted above note amount.", debitNoteOverAdjusted);
            AddIssueIf(issues, nonCashMissingBank > 0, "Critical", "VENDOR_PAYMENT_BANK_MAPPING_MISSING", vendor, $"{nonCashMissingBank} non-cash vendor payment row(s) are missing bank/POS/UPI mapping.", nonCashMissingBank);
            AddIssueIf(issues, missingItemRows > 0, "Warning", "PURCHASE_ITEM_ROWS_MISSING", vendor, $"{missingItemRows} purchase invoice(s) have no item rows.", missingItemRows);
            AddIssueIf(issues, purchaseJournalMissing > 0, "Warning", "PURCHASE_JOURNAL_MISSING", vendor, $"{purchaseJournalMissing} purchase invoice(s) are missing PurchaseInvoice journal evidence.", purchaseJournalMissing);
            AddIssueIf(issues, voucherMissing > 0, "Warning", "VENDOR_PAYMENT_VOUCHER_MISSING", vendor, $"{voucherMissing} vendor payment row(s) should have voucher evidence but VoucherId is empty.", voucherMissing);
            AddIssueIf(issues, inactiveVoucher > 0, "Warning", "VENDOR_PAYMENT_VOUCHER_INACTIVE", vendor, $"{inactiveVoucher} vendor payment row(s) point to missing/deleted voucher evidence.", inactiveVoucher);
            AddIssueIf(issues, voucherAmountMismatch > 0, "Warning", "VENDOR_PAYMENT_VOUCHER_AMOUNT_MISMATCH", vendor, $"{voucherAmountMismatch} vendor payment voucher(s) have amount different from payment row.", voucherAmountMismatch);
            AddIssueIf(issues, voucherModeMismatch > 0, "Warning", "VENDOR_PAYMENT_VOUCHER_MODE_MISMATCH", vendor, $"{voucherModeMismatch} vendor payment voucher(s) have payment mode different from payment row.", voucherModeMismatch);
            AddIssueIf(issues, voucherJournalMissing > 0, "Warning", "VENDOR_PAYMENT_JOURNAL_MISSING", vendor, $"{voucherJournalMissing} vendor payment voucher(s) are missing VendorPaymentVoucher journal evidence.", voucherJournalMissing);
            AddIssueIf(issues, debitNoteUnprintedOpen > 0, "Warning", "VENDOR_OPEN_DEBIT_NOTE_NOT_PRINTED", vendor, $"{debitNoteUnprintedOpen} open vendor debit note(s) are not marked printed/shared.", debitNoteUnprintedOpen);
            AddIssueIf(issues, debitNoteSettlementMismatch > 0, "Warning", "VENDOR_DEBIT_NOTE_SETTLEMENT_MISMATCH", vendor, $"{debitNoteSettlementMismatch} debit note(s) have adjusted amount different from posted settlement total.", debitNoteSettlementMismatch);
            AddIssueIf(issues, openDebitNoteAmount > AmountTolerance, "Warning", "VENDOR_OPEN_DEBIT_NOTE_AVAILABLE", vendor, $"Open debit-note credit {Money(openDebitNoteAmount)} is available to adjust against vendor payable.", openDebitNoteAmount);

            evidenceRows.Add(new VendorPayableEvidenceRowDto(
                vendor.Id,
                vendor.Name,
                vendor.MobileNumber,
                vendor.GSTIN,
                vendorInvoices.Count,
                vendorInvoices.Count(item => Math.Max(item.BillAmount - (paymentsByInvoice.TryGetValue(item.Id, out var rows) ? rows.Sum(row => row.Amount) : 0m), 0m) > AmountTolerance),
                invoiceTotal,
                invoicePaid,
                activePaymentTotal,
                invoiceOutstanding,
                vendor.BillAmount,
                vendor.Paid,
                masterBalance,
                openDebitNoteAmount,
                payableAfterOpenDebitNotes,
                expectedBalance,
                balanceDifference,
                advancePaymentTotal,
                debitNoteSettlementPayment,
                vendorDebitNotes.Count,
                vendorSettlements.Count,
                invoiceStatusMismatch,
                paidStatusWithBalance,
                overpaidInvoices,
                nonCashMissingBank,
                voucherMissing + inactiveVoucher + voucherAmountMismatch + voucherModeMismatch + voucherJournalMissing,
                debitNoteOverAdjusted,
                debitNoteUnprintedOpen,
                BuildVendorStatus(masterBillDifference, masterPaidDifference, balanceDifference, invoiceStatusMismatch, paidStatusWithBalance, overpaidInvoices, debitNoteOverAdjusted, nonCashMissingBank, missingItemRows, purchaseJournalMissing, voucherMissing, inactiveVoucher, voucherAmountMismatch, voucherModeMismatch, voucherJournalMissing, debitNoteUnprintedOpen, debitNoteSettlementMismatch, openDebitNoteAmount)));
        }

        var criticalIssues = issues.Count(item => item.Severity == "Critical");
        var warningIssues = issues.Count(item => item.Severity == "Warning");
        var status = criticalIssues == 0 && warningIssues == 0 ? "Complete" : "Not Complete";
        var totalInvoiceAmount = Round(evidenceRows.Sum(item => item.InvoiceTotal));
        var totalPaymentRows = Round(evidenceRows.Sum(item => item.ActivePaymentRows));
        var totalOutstanding = Round(evidenceRows.Sum(item => item.InvoiceOutstanding));
        var totalOpenDebitNotes = Round(evidenceRows.Sum(item => item.OpenDebitNoteAmount));
        var totalExpectedPayable = Round(evidenceRows.Sum(item => item.ExpectedPayableBalance));

        var metrics = new List<VendorPayableMetricDto>
        {
            new("Vendors checked", evidenceRows.Count, null, "Vendors with purchase invoice, payment, debit-note, settlement, or active payable evidence."),
            new("Purchase invoice total", evidenceRows.Sum(item => item.InvoiceCount), totalInvoiceAmount, "Active purchase invoices up to the selected close date."),
            new("Payment rows", payments.Count, totalPaymentRows, "Active vendor payment rows including invoice payments, advances and debit-note settlements."),
            new("Open outstanding", evidenceRows.Sum(item => item.OpenInvoiceCount), totalOutstanding, "Purchase invoice balance before open debit-note adjustment."),
            new("Open debit notes", debitNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance), totalOpenDebitNotes, "Vendor debit notes still available for adjustment/refund."),
            new("Expected payable", evidenceRows.Count(item => Math.Abs(item.ExpectedPayableBalance) > AmountTolerance), totalExpectedPayable, "Invoice outstanding minus open debit notes."),
            new("Critical issues", criticalIssues, null, "Must be cleared before vendor payable closeout."),
            new("Warnings", warningIssues, null, "Review before final sign-off.")
        };

        var paymentSources = new List<VendorPaymentSourceDto>
        {
            new("Invoice payments", payments.Count(item => item.PurchaseInvoiceId != Guid.Empty && !IsDebitNoteSettlementPayment(item)), Round(payments.Where(item => item.PurchaseInvoiceId != Guid.Empty && !IsDebitNoteSettlementPayment(item)).Sum(item => item.Amount)), payments.Count(item => item.PurchaseInvoiceId != Guid.Empty && item.OnDate >= fromDate && item.OnDate < toExclusive && !IsDebitNoteSettlementPayment(item)), Round(payments.Where(item => item.PurchaseInvoiceId != Guid.Empty && item.OnDate >= fromDate && item.OnDate < toExclusive && !IsDebitNoteSettlementPayment(item)).Sum(item => item.Amount))),
            new("Vendor advances", payments.Count(IsVendorAdvance), Round(payments.Where(IsVendorAdvance).Sum(item => item.Amount)), payments.Count(item => IsVendorAdvance(item) && item.OnDate >= fromDate && item.OnDate < toExclusive), Round(payments.Where(item => IsVendorAdvance(item) && item.OnDate >= fromDate && item.OnDate < toExclusive).Sum(item => item.Amount))),
            new("Debit-note settlements", payments.Count(IsDebitNoteSettlementPayment), Round(payments.Where(IsDebitNoteSettlementPayment).Sum(item => item.Amount)), payments.Count(item => IsDebitNoteSettlementPayment(item) && item.OnDate >= fromDate && item.OnDate < toExclusive), Round(payments.Where(item => IsDebitNoteSettlementPayment(item) && item.OnDate >= fromDate && item.OnDate < toExclusive).Sum(item => item.Amount))),
            new("Vendor refund settlements", settlements.Count(item => item.RefundAmount > 0), Round(settlements.Sum(item => item.RefundAmount)), settlements.Count(item => item.RefundAmount > 0 && item.OnDate >= fromDate && item.OnDate < toExclusive), Round(settlements.Where(item => item.RefundAmount > 0 && item.OnDate >= fromDate && item.OnDate < toExclusive).Sum(item => item.RefundAmount)))
        };

        return new VendorPayableReconciliationReportDto(
            status,
            fromDate,
            toDate,
            companyId,
            storeGroupId,
            storeId,
            criticalIssues,
            warningIssues,
            totalInvoiceAmount,
            totalPaymentRows,
            totalOutstanding,
            totalOpenDebitNotes,
            totalExpectedPayable,
            metrics,
            paymentSources,
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).ThenBy(item => item.VendorName).Take(EvidenceLimit).ToList(),
            evidenceRows.OrderByDescending(item => item.Status == "Critical" ? 2 : item.Status == "Warning" ? 1 : 0).ThenByDescending(item => Math.Abs(item.BalanceDifference)).ThenByDescending(item => item.InvoiceOutstanding).Take(EvidenceLimit).ToList(),
            CloseoutChecklist(status),
            OperatorRules(),
            KnownLimitations(),
            NextModuleCandidates());
    }

    private static bool IsVendorAdvance(PurchasePayment payment)
    {
        return payment.PurchaseInvoiceId == Guid.Empty || string.Equals(payment.AdjustmentSourceType, "VendorAdvance", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDebitNoteSettlementPayment(PurchasePayment payment)
    {
        return payment.PaymentMode == PaymentMode.DebitNote || string.Equals(payment.AdjustmentSourceType, "VendorSettlement", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInitialPurchaseInwardPayment(PurchasePayment payment)
    {
        return string.Equals(payment.Remarks, "Purchase inward payment", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldHaveVoucher(PurchasePayment payment)
    {
        return payment.Amount > 0
            && !IsInitialPurchaseInwardPayment(payment)
            && !IsDebitNoteSettlementPayment(payment);
    }

    private static InvoiceStatus ExpectedInvoiceStatus(decimal billAmount, decimal paidAmount)
    {
        if (paidAmount <= AmountTolerance) return InvoiceStatus.Pending;
        return paidAmount + AmountTolerance >= billAmount ? InvoiceStatus.Paid : InvoiceStatus.PartiallyPaid;
    }

    private static bool RequiresBankMapping(PaymentMode paymentMode)
    {
        return paymentMode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;
    }

    private static string BuildVendorStatus(
        decimal masterBillDifference,
        decimal masterPaidDifference,
        decimal balanceDifference,
        int invoiceStatusMismatch,
        int paidStatusWithBalance,
        int overpaidInvoices,
        int debitNoteOverAdjusted,
        int nonCashMissingBank,
        int missingItemRows,
        int purchaseJournalMissing,
        int voucherMissing,
        int inactiveVoucher,
        int voucherAmountMismatch,
        int voucherModeMismatch,
        int voucherJournalMissing,
        int debitNoteUnprintedOpen,
        int debitNoteSettlementMismatch,
        decimal openDebitNoteAmount)
    {
        if (Math.Abs(masterBillDifference) > AmountTolerance || Math.Abs(masterPaidDifference) > AmountTolerance || Math.Abs(balanceDifference) > AmountTolerance || invoiceStatusMismatch > 0 || paidStatusWithBalance > 0 || overpaidInvoices > 0 || debitNoteOverAdjusted > 0 || nonCashMissingBank > 0)
        {
            return "Critical";
        }

        if (missingItemRows > 0 || purchaseJournalMissing > 0 || voucherMissing > 0 || inactiveVoucher > 0 || voucherAmountMismatch > 0 || voucherModeMismatch > 0 || voucherJournalMissing > 0 || debitNoteUnprintedOpen > 0 || debitNoteSettlementMismatch > 0 || openDebitNoteAmount > AmountTolerance)
        {
            return "Warning";
        }

        return "Pass";
    }

    private static void AddIssueIf(List<VendorPayableIssueDto> issues, bool condition, string severity, string code, Vendor vendor, string message, decimal? amount)
    {
        if (!condition) return;
        issues.Add(new VendorPayableIssueDto(severity, code, vendor.Id, vendor.Name, vendor.MobileNumber, vendor.GSTIN, message, amount));
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

    private static string Money(decimal value) => $"₹{value:N2}";

    private static IReadOnlyList<string> CloseoutChecklist(string status)
    {
        var lines = new List<string>
        {
            "Run vendor payable reconciliation for the exact purchase closeout date before vendor balance sign-off.",
            "Clear vendor master BillAmount/Paid differences before accepting payable balance.",
            "Confirm every purchase invoice status matches active payment-row evidence.",
            "Confirm non-cash vendor payments and advances have bank/POS/UPI mapping.",
            "Adjust or settle open vendor debit notes through Vendor Settlements before month close.",
            "Verify vendor payment vouchers and accounting journals for non-inward payments.",
            "Export CSV and attach it with Day Book, Purchase Import and Accounting/GST validation evidence."
        };

        if (status != "Complete")
        {
            lines.Insert(0, "Status is Not Complete. Do not close vendor payable until blockers are corrected.");
        }

        return lines;
    }

    private static IReadOnlyList<string> OperatorRules() => new[]
    {
        "Do not manually overwrite vendor BillAmount or Paid to hide differences; use purchase invoice, vendor payment, debit-note settlement or controlled repair.",
        "Vendor advance payments must remain visible as advance evidence until adjusted against invoices or refunded by supplier.",
        "Debit notes from purchase returns must be printed/shared and then settled through Vendor Settlements.",
        "Non-cash vendor payments require bank/POS/UPI mapping before day closing and accounting review.",
        "Wrong vendor payment entries should be corrected through Vendor Payments edit/delete so voucher, bank and accounting artifacts are reversed/reposted."
    };

    private static IReadOnlyList<string> KnownLimitations() => new[]
    {
        "This is a validation and evidence layer; it does not automatically repair balances.",
        "The report computes payable up to the selected close date; vendor master balances are current master values and are most reliable when the close date is today/current closeout date.",
        "Legacy purchase rows without item or journal evidence may need Data Consistency Repair before status becomes Complete.",
        "Initial payment captured inside purchase inward is allowed without a separate vendor payment voucher because it is posted through the PurchaseInvoice journal."
    };

    private static IReadOnlyList<string> NextModuleCandidates() => new[]
    {
        "Goods return/exchange operational acceptance with credit-note expiry tracking",
        "Financial year closeout dashboard",
        "Stock valuation and profit/loss final evidence",
        "Commercial Summary naming and CRM/Loyalty UX polish"
    };

    private static string BuildCsv(VendorPayableReconciliationReportDto report)
    {
        var sb = new StringBuilder();
        void Row(params object?[] values) => sb.AppendLine(string.Join(',', values.Select(Csv)));

        Row("Garmetix Vendor Payable / Purchase Settlement Reconciliation");
        Row("Status", report.Status, "From", report.From.ToString("yyyy-MM-dd"), "To", report.To.ToString("yyyy-MM-dd"));
        Row("Critical", report.CriticalIssues, "Warnings", report.WarningIssues, "Invoice Total", report.TotalInvoiceAmount, "Payment Rows", report.TotalPaymentRows, "Outstanding", report.TotalOutstanding, "Open Debit Notes", report.TotalOpenDebitNotes, "Expected Payable", report.TotalExpectedPayable);
        Row();
        Row("Metrics");
        Row("Label", "Count", "Amount", "Description");
        foreach (var metric in report.Metrics)
        {
            Row(metric.Label, metric.Count, metric.Amount, metric.Description);
        }

        Row();
        Row("Payment / Settlement Sources");
        Row("Source", "Total Rows", "Total Amount", "Period Rows", "Period Amount");
        foreach (var source in report.PaymentSources)
        {
            Row(source.Source, source.TotalRows, source.TotalAmount, source.PeriodRows, source.PeriodAmount);
        }

        Row();
        Row("Issues");
        Row("Severity", "Code", "Vendor", "Mobile", "GSTIN", "Message", "Amount");
        foreach (var issue in report.Issues)
        {
            Row(issue.Severity, issue.Code, issue.VendorName, issue.MobileNumber, issue.Gstin, issue.Message, issue.Amount);
        }

        Row();
        Row("Vendor Evidence");
        Row("Vendor", "Mobile", "GSTIN", "Invoices", "Open Invoices", "Invoice Total", "Invoice Paid", "Payment Rows", "Outstanding", "Master Bill", "Master Paid", "Master Balance", "Open Debit Notes", "Expected Payable", "Balance Diff", "Advance Paid", "Debit Note Settled", "Debit Notes", "Settlements", "Status Mismatch", "Paid With Balance", "Overpaid", "Bank Missing", "Voucher Issues", "Debit Overadjusted", "Open DN Not Printed", "Status");
        foreach (var row in report.EvidenceRows)
        {
            Row(row.VendorName, row.MobileNumber, row.Gstin, row.InvoiceCount, row.OpenInvoiceCount, row.InvoiceTotal, row.InvoicePaid, row.ActivePaymentRows, row.InvoiceOutstanding, row.MasterBillAmount, row.MasterPaidAmount, row.MasterBalance, row.OpenDebitNoteAmount, row.ExpectedPayableBalance, row.BalanceDifference, row.AdvancePaymentAmount, row.DebitNoteSettlementAmount, row.DebitNoteCount, row.SettlementCount, row.InvoiceStatusMismatchCount, row.PaidStatusWithBalanceCount, row.OverpaidInvoiceCount, row.NonCashMissingBankCount, row.VoucherIssueCount, row.DebitNoteOverAdjustedCount, row.DebitNoteUnprintedOpenCount, row.Status);
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
            _ => value.ToString() ?? string.Empty
        };
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}

public record VendorPayableReconciliationReportDto(
    string Status,
    DateTime From,
    DateTime To,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    int CriticalIssues,
    int WarningIssues,
    decimal TotalInvoiceAmount,
    decimal TotalPaymentRows,
    decimal TotalOutstanding,
    decimal TotalOpenDebitNotes,
    decimal TotalExpectedPayable,
    IReadOnlyList<VendorPayableMetricDto> Metrics,
    IReadOnlyList<VendorPaymentSourceDto> PaymentSources,
    IReadOnlyList<VendorPayableIssueDto> Issues,
    IReadOnlyList<VendorPayableEvidenceRowDto> EvidenceRows,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public record VendorPayableMetricDto(string Label, int Count, decimal? Amount, string Description);
public record VendorPaymentSourceDto(string Source, int TotalRows, decimal TotalAmount, int PeriodRows, decimal PeriodAmount);
public record VendorPayableIssueDto(string Severity, string Code, Guid VendorId, string VendorName, string? MobileNumber, string? Gstin, string Message, decimal? Amount);
public record VendorPayableEvidenceRowDto(
    Guid VendorId,
    string VendorName,
    string? MobileNumber,
    string? Gstin,
    int InvoiceCount,
    int OpenInvoiceCount,
    decimal InvoiceTotal,
    decimal InvoicePaid,
    decimal ActivePaymentRows,
    decimal InvoiceOutstanding,
    decimal MasterBillAmount,
    decimal MasterPaidAmount,
    decimal MasterBalance,
    decimal OpenDebitNoteAmount,
    decimal PayableAfterOpenDebitNotes,
    decimal ExpectedPayableBalance,
    decimal BalanceDifference,
    decimal AdvancePaymentAmount,
    decimal DebitNoteSettlementAmount,
    int DebitNoteCount,
    int SettlementCount,
    int InvoiceStatusMismatchCount,
    int PaidStatusWithBalanceCount,
    int OverpaidInvoiceCount,
    int NonCashMissingBankCount,
    int VoucherIssueCount,
    int DebitNoteOverAdjustedCount,
    int DebitNoteUnprintedOpenCount,
    string Status);
