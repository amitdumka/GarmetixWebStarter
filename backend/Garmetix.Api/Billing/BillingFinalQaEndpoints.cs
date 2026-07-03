using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Billing;

public static class BillingFinalQaEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceRowLimit = 150;

    public static RouteGroupBuilder MapBillingFinalQaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/billing/final-qa")
            .WithTags("Billing Final QA")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("", GetFinalQaAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<BillingFinalQaDto> GetFinalQaAsync(
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
        var fileName = $"garmetix-billing-final-qa-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<BillingFinalQaDto> BuildReportAsync(
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
        var invoiceQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);

        if (companyId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        }

        if (storeId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(item => item.StoreId == storeId.Value);
        }

        var invoices = await invoiceQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).Distinct().ToList();
        var invoiceIdSet = invoiceIds.ToHashSet();

        var payments = invoiceIds.Count == 0
            ? new List<InvoicePayment>()
            : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && invoiceIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var items = invoiceIds.Count == 0
            ? new List<InvoiceItem>()
            : await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context)
                .Where(item => !item.Deleted && invoiceIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var journals = invoiceIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.JournalEntry>()
            : await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceId.HasValue && invoiceIds.Contains(item.SourceId.Value)
                    && (item.SourceType == "SalesInvoice" || item.SourceType == "SalesInvoiceCancellation" || item.SourceType == "SalesReturn" || item.SourceType == "SalesExchange"))
                .ToListAsync(cancellationToken);

        var stockMovements = invoiceIds.Count == 0
            ? new List<Garmetix.Core.Models.Inventory.StockMovement>()
            : await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceId.HasValue && invoiceIds.Contains(item.SourceId.Value)
                    && (item.SourceType == "SalesInvoice" || item.SourceType == "SalesInvoiceCancellation" || item.SourceType == "SalesReturn" || item.SourceType == "SalesExchange"))
                .ToListAsync(cancellationToken);

        var originalIds = invoices
            .Where(item => item.OriginalInvoiceId.HasValue)
            .Select(item => item.OriginalInvoiceId!.Value)
            .Distinct()
            .ToList();

        var originals = originalIds.Count == 0
            ? new Dictionary<Guid, Invoice>()
            : await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => originalIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var replacementAuditCount = await WorkspaceScope.ApplyTo(db.AuditLogEntries.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.Module == "Invoice Replacement" && item.OccurredAt >= fromDate && item.OccurredAt < toExclusive)
            .CountAsync(cancellationToken);

        var paymentsByInvoice = payments.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var itemsByInvoice = items.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var journalsByInvoice = journals.GroupBy(item => item.SourceId!.Value).ToDictionary(group => group.Key, group => group.ToList());
        var stockByInvoice = stockMovements.GroupBy(item => item.SourceId!.Value).ToDictionary(group => group.Key, group => group.ToList());

        var issues = new List<BillingFinalQaIssueDto>();
        var evidenceRows = new List<BillingFinalQaInvoiceEvidenceDto>();

        foreach (var invoice in invoices)
        {
            var invoicePayments = paymentsByInvoice.TryGetValue(invoice.Id, out var paymentRows) ? paymentRows : new List<InvoicePayment>();
            var invoiceItems = itemsByInvoice.TryGetValue(invoice.Id, out var itemRows) ? itemRows : new List<InvoiceItem>();
            var invoiceJournals = journalsByInvoice.TryGetValue(invoice.Id, out var journalRows) ? journalRows : new List<Garmetix.Core.Models.Accounting.JournalEntry>();
            var invoiceStock = stockByInvoice.TryGetValue(invoice.Id, out var stockRows) ? stockRows : new List<Garmetix.Core.Models.Inventory.StockMovement>();

            var paymentRowsTotal = Round(invoicePayments.Sum(item => item.Amount));
            var itemAmount = Round(invoiceItems.Sum(item => item.Amount));
            var itemTax = Round(invoiceItems.Sum(item => item.TaxAmount));
            var itemQuantity = Round(invoiceItems.Sum(item => item.BilledQuantity));
            var missingBarcodeLines = invoiceItems.Count(item => string.IsNullOrWhiteSpace(item.Barcode));
            var invalidQtyLines = invoiceItems.Count(item => item.BilledQuantity <= 0);
            var bankMissingRows = invoicePayments.Count(item => item.Amount > 0 && RequiresBankMapping(item.PaymentMode) && !item.BankAccountId.HasValue);
            var paymentRowCount = invoicePayments.Count(item => item.Amount != 0);
            var hasMixedPayment = paymentRowCount > 1 || invoice.PaymentMode == PaymentMode.MixPayments;
            var hasSaleJournal = invoiceJournals.Any(item => item.SourceType == "SalesInvoice");
            var hasCancelJournal = invoiceJournals.Any(item => item.SourceType == "SalesInvoiceCancellation");
            var hasSaleStock = invoiceStock.Any(item => item.SourceType == "SalesInvoice" && item.QuantityOut > 0);
            var isCancelled = invoice.InvoiceStatus == InvoiceStatus.Cancelled;
            var activeSaleNeedsOperationalEvidence = !isCancelled && !invoice.ReturnInvoice;

            AddInvoiceIssueIf(issues, Abs(invoice.PaidAmount - paymentRowsTotal) > AmountTolerance, "Critical", "SALE_PAYMENT_TOTAL_MISMATCH", invoice, $"Invoice PaidAmount {Money(invoice.PaidAmount)} differs from payment rows {Money(paymentRowsTotal)}.", invoice.PaidAmount - paymentRowsTotal);
            AddInvoiceIssueIf(issues, invoice.PaidAmount - invoice.BillAmount > AmountTolerance && !isCancelled, "Critical", "SALE_OVERPAID", invoice, $"Paid amount {Money(invoice.PaidAmount)} is greater than bill amount {Money(invoice.BillAmount)}.", invoice.PaidAmount - invoice.BillAmount);
            AddInvoiceIssueIf(issues, activeSaleNeedsOperationalEvidence && invoiceItems.Count == 0, "Critical", "SALE_MISSING_ITEMS", invoice, "Active sale has no item rows.", null);
            AddInvoiceIssueIf(issues, missingBarcodeLines > 0, "Critical", "SALE_ITEM_BARCODE_MISSING", invoice, $"{missingBarcodeLines} item line(s) are missing barcode evidence.", null);
            AddInvoiceIssueIf(issues, invalidQtyLines > 0, "Critical", "SALE_ITEM_QTY_INVALID", invoice, $"{invalidQtyLines} item line(s) have zero or negative quantity.", null);
            AddInvoiceIssueIf(issues, Abs(invoice.TaxAmount - itemTax) > AmountTolerance && invoiceItems.Count > 0, "Critical", "SALE_GST_SNAPSHOT_MISMATCH", invoice, $"Invoice tax {Money(invoice.TaxAmount)} differs from item tax snapshots {Money(itemTax)}.", invoice.TaxAmount - itemTax);
            AddInvoiceIssueIf(issues, activeSaleNeedsOperationalEvidence && bankMissingRows > 0, "Critical", "SALE_NON_CASH_BANK_MAPPING_MISSING", invoice, $"{bankMissingRows} non-cash payment row(s) need bank/POS/UPI mapping.", null);
            AddInvoiceIssueIf(issues, activeSaleNeedsOperationalEvidence && !hasSaleJournal, "Critical", "SALE_ACCOUNTING_JOURNAL_MISSING", invoice, "Active sale has no SalesInvoice accounting journal.", null);
            AddInvoiceIssueIf(issues, activeSaleNeedsOperationalEvidence && invoiceItems.Count > 0 && !hasSaleStock, "Critical", "SALE_STOCK_OUT_MISSING", invoice, "Active sale item rows exist but no SalesInvoice stock-out movement was found.", null);
            AddInvoiceIssueIf(issues, hasMixedPayment && paymentRowCount < 2, "Warning", "SALE_MIXED_PAYMENT_WITHOUT_SPLIT_ROWS", invoice, "Invoice is marked or treated as mixed-payment but does not have at least two payment rows.", null);
            AddInvoiceIssueIf(issues, !hasMixedPayment && paymentRowCount > 1, "Warning", "SALE_SPLIT_ROWS_WITHOUT_MIXED_MODE", invoice, "Invoice has multiple payment rows but invoice-level payment mode is not MixPayments.", null);
            AddInvoiceIssueIf(issues, activeSaleNeedsOperationalEvidence && invoice.PaidAmount + AmountTolerance < invoice.BillAmount && invoice.InvoiceStatus == InvoiceStatus.Paid, "Warning", "SALE_STATUS_PAID_WITH_BALANCE", invoice, "Invoice status is Paid but customer balance still exists.", invoice.BillAmount - invoice.PaidAmount);
            AddInvoiceIssueIf(issues, isCancelled && !hasCancelJournal && invoice.PaidAmount > 0, "Warning", "SALE_CANCEL_JOURNAL_REVIEW", invoice, "Cancelled paid invoice has no SalesInvoiceCancellation journal in the selected evidence.", null);

            if (invoice.OriginalInvoiceId.HasValue && originals.TryGetValue(invoice.OriginalInvoiceId.Value, out var original) && original.InvoiceStatus != InvoiceStatus.Cancelled && !isCancelled)
            {
                AddInvoiceIssueIf(issues, true, "Critical", "SALE_REPLACEMENT_PENDING_APPROVAL", invoice, $"Revised sale {invoice.InvoiceNumber} still needs replacement approval/reversal of original sale {original.InvoiceNumber}.", null);
            }

            var invoiceChecks = new List<string>
            {
                activeSaleNeedsOperationalEvidence ? (invoiceItems.Count > 0 ? "Pass: items" : "Fail: missing items") : "Info: cancelled/return sale",
                Abs(invoice.PaidAmount - paymentRowsTotal) <= AmountTolerance ? "Pass: paid rows match" : "Fail: paid rows mismatch",
                missingBarcodeLines == 0 ? "Pass: barcode evidence" : "Fail: barcode missing",
                bankMissingRows == 0 ? "Pass: bank mapped" : "Fail: bank mapping missing",
                !activeSaleNeedsOperationalEvidence || hasSaleJournal ? "Pass: accounting journal" : "Fail: journal missing",
                !activeSaleNeedsOperationalEvidence || invoiceItems.Count == 0 || hasSaleStock ? "Pass: stock-out evidence" : "Fail: stock-out missing"
            };

            evidenceRows.Add(new BillingFinalQaInvoiceEvidenceDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.OnDate,
                invoice.CustomerName ?? "Walk-in Customer",
                invoice.InvoiceStatus.ToString(),
                invoice.PaymentMode?.ToString() ?? "Unspecified",
                invoice.BillAmount,
                invoice.PaidAmount,
                paymentRowsTotal,
                Round(invoice.BillAmount - invoice.PaidAmount),
                itemAmount,
                itemTax,
                itemQuantity,
                paymentRowCount,
                hasMixedPayment,
                bankMissingRows,
                missingBarcodeLines,
                hasSaleJournal,
                hasSaleStock,
                invoice.OriginalInvoiceId.HasValue,
                $"/billing?invoiceId={invoice.Id}",
                invoiceChecks));
        }

        if (invoices.Count == 0)
        {
            issues.Add(new BillingFinalQaIssueDto("Warning", "SALE_NO_INVOICES_IN_PERIOD", "No sale invoices were found in the selected period. Run a real sale QA period before marking billing complete.", null, null, null));
        }

        var activeInvoices = invoices.Where(item => item.InvoiceStatus != InvoiceStatus.Cancelled).ToList();
        var cancelledInvoices = invoices.Count(item => item.InvoiceStatus == InvoiceStatus.Cancelled);
        var mixedPaymentInvoices = evidenceRows.Count(item => item.HasMixedPayment);
        var pendingReplacementCount = issues.Count(item => item.Code == "SALE_REPLACEMENT_PENDING_APPROVAL");
        var bankMissingTotal = evidenceRows.Sum(item => item.MissingBankMappingRows);
        var missingBarcodeTotal = evidenceRows.Sum(item => item.MissingBarcodeLines);
        var paymentMismatchCount = issues.Count(item => item.Code == "SALE_PAYMENT_TOTAL_MISMATCH");
        var gstMismatchCount = issues.Count(item => item.Code == "SALE_GST_SNAPSHOT_MISMATCH");
        var missingJournalCount = issues.Count(item => item.Code == "SALE_ACCOUNTING_JOURNAL_MISSING");
        var missingStockCount = issues.Count(item => item.Code == "SALE_STOCK_OUT_MISSING");

        var paymentSummary = payments
            .GroupBy(item => item.PaymentMode)
            .OrderBy(group => group.Key.ToString())
            .Select(group => new BillingFinalQaPaymentModeDto(
                group.Key.ToString(),
                group.Count(),
                Round(group.Sum(item => item.Amount)),
                group.Count(item => item.Amount > 0 && RequiresBankMapping(item.PaymentMode) && !item.BankAccountId.HasValue),
                group.Count(item => !string.IsNullOrWhiteSpace(item.ReferenceNumber) || !string.IsNullOrWhiteSpace(item.GatewayReference))))
            .ToList();

        var gstSummary = items
            .GroupBy(item => item.TaxPercentage)
            .OrderBy(group => group.Key)
            .Select(group => new BillingFinalQaGstRateDto(
                group.Key,
                Round(group.Sum(item => item.BasePrice)),
                Round(group.Sum(item => item.TaxAmount)),
                Round(group.Sum(item => item.Amount)),
                Round(group.Sum(item => item.BilledQuantity)),
                group.Count()))
            .ToList();

        var criticalIssues = issues.Count(item => item.Severity == "Critical");
        var warningIssues = issues.Count(item => item.Severity == "Warning");
        var status = criticalIssues == 0 && warningIssues == 0 ? "Complete" : "Not Complete";

        var metrics = new List<BillingFinalQaMetricDto>
        {
            new("Active invoices", null, activeInvoices.Count, "Non-cancelled sale invoices in selected period."),
            new("Cancelled invoices", null, cancelledInvoices, "Cancelled sale invoices needing reversal evidence."),
            new("Bill total", Round(activeInvoices.Sum(item => item.BillAmount)), null, "Active invoice bill amount."),
            new("Paid total", Round(activeInvoices.Sum(item => item.PaidAmount)), null, "Active invoice paid amount."),
            new("Payment rows total", Round(payments.Where(item => invoiceIdSet.Contains(item.InvoiceId)).Sum(item => item.Amount)), null, "Total from InvoicePayments rows."),
            new("Customer balance", Round(activeInvoices.Sum(item => item.BillAmount - item.PaidAmount)), null, "Remaining receivable from selected active sales."),
            new("Mixed-payment invoices", null, mixedPaymentInvoices, "Invoices with split payment rows or MixPayments mode."),
            new("Payment mismatches", null, paymentMismatchCount, "PaidAmount vs payment rows mismatch."),
            new("Missing bank mappings", null, bankMissingTotal, "Non-cash rows without bank/POS account."),
            new("GST mismatches", null, gstMismatchCount, "Invoice tax vs item tax snapshot mismatch."),
            new("Missing barcode lines", null, missingBarcodeTotal, "Invoice item barcode evidence gaps."),
            new("Pending replacements", null, pendingReplacementCount, "Revised invoices whose original invoice is not cancelled yet."),
            new("Missing journals", null, missingJournalCount, "Active sales without SalesInvoice journal."),
            new("Missing stock-outs", null, missingStockCount, "Active sales without stock-out evidence."),
            new("Replacement audit rows", null, replacementAuditCount, "Approval/reversal audit rows in selected period.")
        };

        var checks = new List<BillingFinalQaCheckDto>
        {
            new("Real sale data exists", invoices.Count > 0 ? "Pass" : "Warning", invoices.Count > 0 ? "At least one sale invoice exists in period." : "No sale invoice found for selected QA period."),
            new("Mixed-payment split evidence", mixedPaymentInvoices > 0 ? "Pass" : "Warning", mixedPaymentInvoices > 0 ? "Split payment invoices are visible for QA." : "Create/test one mixed-payment invoice before live closeout."),
            new("Payment rows reconcile", paymentMismatchCount == 0 ? "Pass" : "Critical", $"{paymentMismatchCount} invoice(s) have PaidAmount vs payment row mismatch."),
            new("Non-cash bank mapping", bankMissingTotal == 0 ? "Pass" : "Critical", $"{bankMissingTotal} non-cash payment row(s) need bank mapping."),
            new("GST item snapshots", gstMismatchCount == 0 ? "Pass" : "Critical", $"{gstMismatchCount} invoice(s) have GST snapshot mismatch."),
            new("Barcode evidence", missingBarcodeTotal == 0 ? "Pass" : "Critical", $"{missingBarcodeTotal} item line(s) missing barcode."),
            new("Replacement approvals", pendingReplacementCount == 0 ? "Pass" : "Critical", $"{pendingReplacementCount} revised sale invoice(s) still need approval/reversal."),
            new("Accounting journals", missingJournalCount == 0 ? "Pass" : "Critical", $"{missingJournalCount} active sale invoice(s) missing journal evidence."),
            new("Stock-out evidence", missingStockCount == 0 ? "Pass" : "Critical", $"{missingStockCount} active sale invoice(s) missing stock-out evidence.")
        };

        return new BillingFinalQaDto(
            fromDate,
            toDate,
            status,
            criticalIssues,
            warningIssues,
            metrics,
            checks,
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).ThenBy(item => item.Code).Take(300).ToList(),
            paymentSummary,
            gstSummary,
            evidenceRows.OrderByDescending(item => item.OnDate).ThenByDescending(item => item.InvoiceNumber).Take(EvidenceRowLimit).ToList(),
            CloseoutChecklist(),
            KnownLimitations(),
            OperatorRules(),
            NextModuleCandidates());
    }

    private static (DateTime From, DateTime To, DateTime ToExclusive) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toDate = (to ?? new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month))).Date;
        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        return (fromDate, toDate, toDate.AddDays(1));
    }

    private static void AddInvoiceIssueIf(List<BillingFinalQaIssueDto> issues, bool condition, string severity, string code, Invoice invoice, string message, decimal? amount)
    {
        if (!condition)
        {
            return;
        }

        issues.Add(new BillingFinalQaIssueDto(severity, code, message, invoice.InvoiceNumber, $"/billing?invoiceId={invoice.Id}", amount));
    }

    private static bool RequiresBankMapping(PaymentMode mode)
        => mode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Abs(decimal value) => Math.Abs(value);
    private static string Money(decimal value) => $"₹{Round(value):0.00}";

    private static IReadOnlyList<string> CloseoutChecklist() => new[]
    {
        "Create and verify one cash-only sale invoice.",
        "Create and verify one non-cash sale invoice with exact bank/POS/UPI account mapping.",
        "Create and verify one mixed-payment invoice, for example cash + UPI, and confirm payment rows match bill collection.",
        "Open the mixed-payment invoice in Day Book and confirm cash and non-cash amounts appear separately.",
        "Revise one invoice through Invoice Replacements and approve the replacement so the old invoice is safely cancelled/reversed.",
        "Verify barcode is present in invoice list, receipt view and printed/PDF invoice.",
        "Verify sale accounting journal, stock-out movement and GST item snapshots for the QA period.",
        "Export this CSV evidence and keep it with daily/monthly closeout proof."
    };

    private static IReadOnlyList<string> KnownLimitations() => new[]
    {
        "This page is a non-mutating validation layer; it does not repair invoices, payment rows, stock movements or journals.",
        "Browser print/PDF visual layout must still be accepted from Print Final Acceptance using a real printer or saved PDF.",
        "Bank settlement matching checks presence of mapped bank/POS account; it does not reconcile bank statement deposits yet.",
        "Old historical invoices created before payment-split fixes may need Data Consistency repair or manual admin review before closure.",
        "Replacement approval must be performed from Invoice Replacements; this page only flags pending revised invoices."
    };

    private static IReadOnlyList<string> OperatorRules() => new[]
    {
        "Do not hard-delete live sale invoices unless they are cancelled test invoices and owner/admin approves.",
        "For wrong GST, price or payment after printing, use controlled revision/replacement instead of editing live evidence directly.",
        "Every non-cash receipt row should carry the exact bank/POS/UPI settlement account and reference whenever available.",
        "Mixed-payment sale must have separate payment rows; invoice-level MixPayments alone is not enough for cash closing.",
        "Close the day only after Billing Final QA, Day Book, GST validation and Print Acceptance agree for the selected period."
    };

    private static IReadOnlyList<string> NextModuleCandidates() => new[]
    {
        "Customer dues / credit balance final reconciliation",
        "Bank settlement and payment mode reconciliation",
        "Sale return and exchange final acceptance",
        "Stock valuation / profit invoice-wise validation"
    };

    private static string BuildCsv(BillingFinalQaDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Section,Field,Value,Detail");
        sb.AppendLine(CsvRow("Summary", "Status", report.Status, $"Critical {report.CriticalIssues}; Warning {report.WarningIssues}"));
        sb.AppendLine(CsvRow("Summary", "From", report.From.ToString("yyyy-MM-dd"), string.Empty));
        sb.AppendLine(CsvRow("Summary", "To", report.To.ToString("yyyy-MM-dd"), string.Empty));

        foreach (var metric in report.Metrics)
        {
            sb.AppendLine(CsvRow("Metric", metric.Label, metric.Amount?.ToString("0.00") ?? metric.Count?.ToString() ?? "0", metric.Detail));
        }

        sb.AppendLine();
        sb.AppendLine("Check,Status,Detail");
        foreach (var check in report.Checks)
        {
            sb.AppendLine(CsvRow(check.Name, check.Status, check.Detail));
        }

        sb.AppendLine();
        sb.AppendLine("Severity,Code,Message,Invoice,Amount,Path");
        foreach (var issue in report.Issues)
        {
            sb.AppendLine(CsvRow(issue.Severity, issue.Code, issue.Message, issue.SourceNumber ?? string.Empty, issue.Amount?.ToString("0.00") ?? string.Empty, issue.SourcePath ?? string.Empty));
        }

        sb.AppendLine();
        sb.AppendLine("PaymentMode,Rows,Amount,MissingBankMappings,ReferenceRows");
        foreach (var row in report.PaymentModes)
        {
            sb.AppendLine(CsvRow(row.PaymentMode, row.RowCount.ToString(), row.Amount.ToString("0.00"), row.MissingBankMappingRows.ToString(), row.ReferenceRows.ToString()));
        }

        sb.AppendLine();
        sb.AppendLine("TaxRate,Taxable,Tax,Amount,Quantity,LineCount");
        foreach (var row in report.GstRows)
        {
            sb.AppendLine(CsvRow(row.TaxRate.ToString("0.##"), row.TaxableAmount.ToString("0.00"), row.TaxAmount.ToString("0.00"), row.Amount.ToString("0.00"), row.Quantity.ToString("0.##"), row.LineCount.ToString()));
        }

        sb.AppendLine();
        sb.AppendLine("InvoiceId,InvoiceNumber,Date,Customer,Status,PaymentMode,BillAmount,PaidAmount,PaymentRowsTotal,Balance,ItemAmount,ItemTax,Quantity,PaymentRows,MixedPayment,MissingBankMappings,MissingBarcodeLines,HasJournal,HasStockOut,Replacement,Path,Checks");
        foreach (var row in report.InvoiceEvidence)
        {
            sb.AppendLine(CsvRow(
                row.InvoiceId.ToString(),
                row.InvoiceNumber,
                row.OnDate.ToString("yyyy-MM-dd"),
                row.CustomerName,
                row.Status,
                row.PaymentMode,
                row.BillAmount.ToString("0.00"),
                row.PaidAmount.ToString("0.00"),
                row.PaymentRowsTotal.ToString("0.00"),
                row.BalanceAmount.ToString("0.00"),
                row.ItemAmount.ToString("0.00"),
                row.ItemTaxAmount.ToString("0.00"),
                row.Quantity.ToString("0.##"),
                row.PaymentRowCount.ToString(),
                row.HasMixedPayment ? "Yes" : "No",
                row.MissingBankMappingRows.ToString(),
                row.MissingBarcodeLines.ToString(),
                row.HasAccountingJournal ? "Yes" : "No",
                row.HasStockOut ? "Yes" : "No",
                row.HasReplacementLink ? "Yes" : "No",
                row.SourcePath,
                string.Join(" | ", row.Checks)));
        }

        return sb.ToString();
    }

    private static string CsvRow(params string[] values) => string.Join(',', values.Select(CsvEscape));

    private static string CsvEscape(string value)
    {
        var text = value ?? string.Empty;
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }
}

public sealed record BillingFinalQaDto(
    DateTime From,
    DateTime To,
    string Status,
    int CriticalIssues,
    int WarningIssues,
    IReadOnlyList<BillingFinalQaMetricDto> Metrics,
    IReadOnlyList<BillingFinalQaCheckDto> Checks,
    IReadOnlyList<BillingFinalQaIssueDto> Issues,
    IReadOnlyList<BillingFinalQaPaymentModeDto> PaymentModes,
    IReadOnlyList<BillingFinalQaGstRateDto> GstRows,
    IReadOnlyList<BillingFinalQaInvoiceEvidenceDto> InvoiceEvidence,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record BillingFinalQaMetricDto(string Label, decimal? Amount, int? Count, string Detail);
public sealed record BillingFinalQaCheckDto(string Name, string Status, string Detail);
public sealed record BillingFinalQaIssueDto(string Severity, string Code, string Message, string? SourceNumber, string? SourcePath, decimal? Amount);
public sealed record BillingFinalQaPaymentModeDto(string PaymentMode, int RowCount, decimal Amount, int MissingBankMappingRows, int ReferenceRows);
public sealed record BillingFinalQaGstRateDto(decimal TaxRate, decimal TaxableAmount, decimal TaxAmount, decimal Amount, decimal Quantity, int LineCount);

public sealed record BillingFinalQaInvoiceEvidenceDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime OnDate,
    string CustomerName,
    string Status,
    string PaymentMode,
    decimal BillAmount,
    decimal PaidAmount,
    decimal PaymentRowsTotal,
    decimal BalanceAmount,
    decimal ItemAmount,
    decimal ItemTaxAmount,
    decimal Quantity,
    int PaymentRowCount,
    bool HasMixedPayment,
    int MissingBankMappingRows,
    int MissingBarcodeLines,
    bool HasAccountingJournal,
    bool HasStockOut,
    bool HasReplacementLink,
    string SourcePath,
    IReadOnlyList<string> Checks);
