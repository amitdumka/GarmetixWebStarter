using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.GoodsReturn;

public static class GoodsReturnAcceptanceEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 300;

    public static RouteGroupBuilder MapGoodsReturnAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/goods-return/acceptance")
            .WithTags("Goods Return Acceptance")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("", GetAcceptanceAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<GoodsReturnAcceptanceReportDto> GetAcceptanceAsync(
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
        var fileName = $"garmetix-goods-return-acceptance-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<GoodsReturnAcceptanceReportDto> BuildReportAsync(
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
        var today = DateTime.Today;

        var returnQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.ReturnInvoice && item.OnDate >= fromDate && item.OnDate < toExclusive);

        if (companyId.HasValue)
        {
            returnQuery = returnQuery.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeId.HasValue)
        {
            returnQuery = returnQuery.Where(item => item.StoreId == storeId.Value);
        }
        else if (storeGroupId.HasValue)
        {
            returnQuery = returnQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        }

        var returns = await returnQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var returnIds = returns.Select(item => item.Id).Distinct().ToList();
        var originalIds = returns.Where(item => item.OriginalInvoiceId.HasValue).Select(item => item.OriginalInvoiceId!.Value).Distinct().ToList();

        var originals = originalIds.Count == 0
            ? new List<Invoice>()
            : await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => originalIds.Contains(item.Id))
                .ToListAsync(cancellationToken);

        var originalById = originals.ToDictionary(item => item.Id);

        var returnPayments = returnIds.Count == 0
            ? new List<InvoicePayment>()
            : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && returnIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var creditNotes = returnIds.Count == 0
            ? new List<CommercialNote>()
            : await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
                .Where(item => !item.Deleted
                    && item.NoteType == NoteType.CreditNote
                    && item.PartyType == PartyType.Customer
                    && item.SourceType == "SalesReturn"
                    && item.SourceId.HasValue
                    && returnIds.Contains(item.SourceId.Value))
                .ToListAsync(cancellationToken);

        var exchangeInvoices = originalIds.Count == 0
            ? new List<Invoice>()
            : await WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => !item.Deleted
                    && !item.ReturnInvoice
                    && item.OriginalInvoiceId.HasValue
                    && originalIds.Contains(item.OriginalInvoiceId.Value)
                    && item.OnDate >= fromDate.AddDays(-7)
                    && item.OnDate < toExclusive.AddDays(45))
                .ToListAsync(cancellationToken);

        var replacementPaymentRows = returnIds.Count == 0
            ? new List<InvoicePayment>()
            : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted
                    && item.AdjustmentSourceId.HasValue
                    && returnIds.Contains(item.AdjustmentSourceId.Value)
                    && (item.AdjustmentSourceType == "SalesReturnCredit" || item.AdjustmentSourceType == "SalesReturn" || item.PaymentMode == PaymentMode.CreditBalance || item.PaymentMode == PaymentMode.CreditNote || item.PaymentMode == PaymentMode.SaleReturn))
                .ToListAsync(cancellationToken);

        var returnItems = returnIds.Count == 0
            ? new List<InvoiceItem>()
            : await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context)
                .Where(item => !item.Deleted && returnIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var notesByReturn = creditNotes
            .GroupBy(item => item.SourceId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.OnDate).ThenByDescending(item => item.CreatedAt).ToList());

        var paymentsByReturn = returnPayments
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var replacementPaymentsByReturn = replacementPaymentRows
            .Where(item => item.AdjustmentSourceId.HasValue)
            .GroupBy(item => item.AdjustmentSourceId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var returnItemsByReturn = returnItems
            .GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var exchangesByOriginal = exchangeInvoices
            .Where(item => item.OriginalInvoiceId.HasValue)
            .GroupBy(item => item.OriginalInvoiceId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.OnDate).ThenBy(item => item.CreatedAt).ToList());

        var issues = new List<GoodsReturnIssueDto>();
        var evidenceRows = new List<GoodsReturnEvidenceRowDto>();

        foreach (var returnInvoice in returns)
        {
            originalById.TryGetValue(returnInvoice.OriginalInvoiceId ?? Guid.Empty, out var originalInvoice);
            var noteRows = notesByReturn.TryGetValue(returnInvoice.Id, out var noteList) ? noteList : new List<CommercialNote>();
            var paymentRows = paymentsByReturn.TryGetValue(returnInvoice.Id, out var refundRows) ? refundRows : new List<InvoicePayment>();
            var replacementPayment = replacementPaymentsByReturn.TryGetValue(returnInvoice.Id, out var replacementRows) ? replacementRows : new List<InvoicePayment>();
            var itemRows = returnItemsByReturn.TryGetValue(returnInvoice.Id, out var invoiceItems) ? invoiceItems : new List<InvoiceItem>();
            var exchangeRows = originalInvoice is not null && exchangesByOriginal.TryGetValue(originalInvoice.Id, out var exchanges) ? exchanges : new List<Invoice>();

            var primaryNote = noteRows.FirstOrDefault();
            var refundAmount = Round(Math.Max(returnInvoice.PaidAmount, paymentRows.Sum(item => item.Amount)));
            var creditedAmount = Round(noteRows.Sum(item => Math.Max(item.Amount - item.AdjustedAmount, 0)));
            var adjustedCredit = Round(noteRows.Sum(item => item.AdjustedAmount));
            var noteTotal = Round(noteRows.Sum(item => item.Amount));
            var overAdjustedNotes = noteRows.Count(item => item.AdjustedAmount - item.Amount > AmountTolerance);
            var unprintedOpenNotes = noteRows.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0) > AmountTolerance && !item.Printed);
            var missingBarcodeItems = itemRows.Count(item => string.IsNullOrWhiteSpace(item.Barcode));
            var exchangeValue = Round(exchangeRows.Sum(item => item.BillAmount));
            var exchangePaidByCredit = Round(replacementPayment.Sum(item => item.Amount));

            var daysFromSale = originalInvoice is null ? (int?)null : (int)Math.Floor((returnInvoice.OnDate.Date - originalInvoice.OnDate.Date).TotalDays);
            var withinPolicyDays = daysFromSale.HasValue && daysFromSale.Value >= 0 && daysFromSale.Value <= 7;
            var expiryDate = primaryNote is null ? (DateTime?)null : CalculateCreditNoteExpiry(primaryNote.OnDate);
            var openCredit = Round(primaryNote is null ? 0 : Math.Max(primaryNote.Amount - primaryNote.AdjustedAmount, 0));
            var expiryStatus = BuildExpiryStatus(primaryNote, expiryDate, today);
            var expiredOpen = primaryNote is not null && openCredit > AmountTolerance && expiryDate.HasValue && expiryDate.Value.Date < today;
            var expiringSoon = primaryNote is not null && openCredit > AmountTolerance && expiryDate.HasValue && expiryDate.Value.Date >= today && expiryDate.Value.Date <= today.AddDays(30);
            var hasImmediateExchange = exchangeRows.Any(item => item.OnDate.Date >= returnInvoice.OnDate.Date && item.OnDate.Date <= returnInvoice.OnDate.Date.AddDays(7));

            AddIssueIf(issues, originalInvoice is null, "Critical", "RETURN_ORIGINAL_INVOICE_MISSING", returnInvoice, "Return/exchange record does not have a valid original sale invoice link.", returnInvoice.BillAmount);
            AddIssueIf(issues, originalInvoice is not null && !withinPolicyDays, "Critical", "RETURN_OUTSIDE_7_DAY_POLICY", returnInvoice, $"Return was created {daysFromSale} day(s) after sale; policy allows exchange/credit only within 7 days.", daysFromSale ?? 0);
            AddIssueIf(issues, refundAmount > AmountTolerance, "Critical", "RETURN_REFUND_BLOCKED_BY_POLICY", returnInvoice, $"Refund/payment evidence of {Money(refundAmount)} exists on a return. Store policy is exchange/credit-note only, no cash refund.", refundAmount);
            AddIssueIf(issues, noteRows.Count == 0, "Critical", "RETURN_CREDIT_NOTE_MISSING", returnInvoice, "Return invoice does not have a linked customer credit note.", returnInvoice.BillAmount);
            AddIssueIf(issues, overAdjustedNotes > 0, "Critical", "RETURN_CREDIT_NOTE_OVERADJUSTED", returnInvoice, $"{overAdjustedNotes} linked credit note(s) are adjusted above note amount.", overAdjustedNotes);
            AddIssueIf(issues, expiredOpen, "Critical", "RETURN_CREDIT_NOTE_EXPIRED_OPEN", returnInvoice, $"Open credit of {Money(openCredit)} is past policy validity date {expiryDate:dd-MMM-yyyy}.", openCredit);
            AddIssueIf(issues, noteRows.Count > 0 && Math.Abs(noteTotal - returnInvoice.BillAmount) > AmountTolerance, "Warning", "RETURN_CREDIT_NOTE_TOTAL_MISMATCH", returnInvoice, $"Linked credit-note total {Money(noteTotal)} differs from return value {Money(returnInvoice.BillAmount)}.", noteTotal - returnInvoice.BillAmount);
            AddIssueIf(issues, unprintedOpenNotes > 0, "Warning", "RETURN_CREDIT_NOTE_NOT_PRINTED", returnInvoice, $"{unprintedOpenNotes} open credit note(s) are not marked printed/shared with customer.", unprintedOpenNotes);
            AddIssueIf(issues, expiringSoon, "Warning", "RETURN_CREDIT_NOTE_EXPIRING_SOON", returnInvoice, $"Open credit of {Money(openCredit)} expires on {expiryDate:dd-MMM-yyyy}.", openCredit);
            AddIssueIf(issues, missingBarcodeItems > 0, "Warning", "RETURN_ITEM_BARCODE_MISSING", returnInvoice, $"{missingBarcodeItems} returned item row(s) are missing barcode evidence.", missingBarcodeItems);
            AddIssueIf(issues, noteRows.Count > 0 && !hasImmediateExchange && openCredit > AmountTolerance, "Warning", "RETURN_CREDIT_NOTE_PENDING_EXCHANGE", returnInvoice, "Return has open store credit and no immediate replacement exchange invoice within 7 days.", openCredit);
            AddIssueIf(issues, exchangeRows.Count > 0 && exchangePaidByCredit <= AmountTolerance, "Warning", "RETURN_EXCHANGE_CREDIT_LINK_MISSING", returnInvoice, "Replacement exchange invoice exists, but no invoice payment row links this return credit to the exchange invoice.", exchangeValue);

            evidenceRows.Add(new GoodsReturnEvidenceRowDto(
                returnInvoice.Id,
                returnInvoice.InvoiceNumber,
                returnInvoice.OnDate,
                originalInvoice?.Id,
                originalInvoice?.InvoiceNumber,
                originalInvoice?.OnDate,
                daysFromSale,
                originalInvoice is null ? "Missing original" : withinPolicyDays ? "Within 7 days" : "Outside 7 days",
                returnInvoice.CustomerId,
                returnInvoice.CustomerName ?? "Walk-in Customer",
                returnInvoice.CustomerMobileNumber,
                returnInvoice.BillAmount,
                refundAmount,
                noteRows.Count,
                primaryNote?.NoteNumber,
                noteTotal,
                adjustedCredit,
                creditedAmount,
                expiryDate,
                expiryStatus,
                exchangeRows.Count,
                string.Join(", ", exchangeRows.Select(item => item.InvoiceNumber).Distinct()),
                exchangeValue,
                exchangePaidByCredit,
                itemRows.Sum(item => item.BilledQuantity),
                missingBarcodeItems,
                BuildRowStatus(originalInvoice, withinPolicyDays, refundAmount, noteRows.Count, overAdjustedNotes, expiredOpen, unprintedOpenNotes, expiringSoon, missingBarcodeItems, hasImmediateExchange, openCredit, exchangeRows.Count, exchangePaidByCredit)));
        }

        var criticalIssues = issues.Count(item => item.Severity == "Critical");
        var warningIssues = issues.Count(item => item.Severity == "Warning");
        var status = criticalIssues == 0 && warningIssues == 0 ? "Complete" : "Not Complete";

        var totalReturnValue = Round(evidenceRows.Sum(item => item.ReturnAmount));
        var totalRefundValue = Round(evidenceRows.Sum(item => item.RefundAmount));
        var totalOpenCredit = Round(evidenceRows.Sum(item => item.CreditNoteOpenAmount));
        var expiredOpenCredit = Round(evidenceRows.Where(item => item.ExpiryStatus == "Expired open").Sum(item => item.CreditNoteOpenAmount));
        var expiringSoonCredit = Round(evidenceRows.Where(item => item.ExpiryStatus == "Expiring soon").Sum(item => item.CreditNoteOpenAmount));
        var exchangedValue = Round(evidenceRows.Sum(item => item.ExchangeInvoiceValue));

        var metrics = new List<GoodsReturnMetricDto>
        {
            new("Return documents", evidenceRows.Count, totalReturnValue, "Sales return/credit documents in selected range."),
            new("Exchange invoices", evidenceRows.Sum(item => item.ExchangeInvoiceCount), exchangedValue, "Replacement exchange sale invoices linked to the original sale."),
            new("Open credit notes", evidenceRows.Count(item => item.CreditNoteOpenAmount > AmountTolerance), totalOpenCredit, "Customer store credit still available for future purchase."),
            new("Refund amount", evidenceRows.Count(item => item.RefundAmount > AmountTolerance), totalRefundValue, "Should remain zero under no-refund policy."),
            new("Expired open credit", evidenceRows.Count(item => item.ExpiryStatus == "Expired open"), expiredOpenCredit, "Credit still open after validity date."),
            new("Expiring within 30 days", evidenceRows.Count(item => item.ExpiryStatus == "Expiring soon"), expiringSoonCredit, "Open credit needing customer/store follow-up.")
        };

        var creditNoteBuckets = new List<GoodsReturnCreditBucketDto>
        {
            new("Same FY validity", evidenceRows.Count(item => item.CreditNoteExpiryDate.HasValue && item.CreditNoteExpiryDate.Value.Month == 3 && item.CreditNoteExpiryDate.Value.Day == 31), Round(evidenceRows.Where(item => item.CreditNoteExpiryDate.HasValue && item.CreditNoteExpiryDate.Value.Month == 3 && item.CreditNoteExpiryDate.Value.Day == 31).Sum(item => item.CreditNoteOpenAmount)), "Credit notes issued April-December, valid up to 31 March of the same financial year."),
            new("Jan/Feb/Mar six-month validity", evidenceRows.Count(item => item.CreditNoteExpiryDate.HasValue && !(item.CreditNoteExpiryDate.Value.Month == 3 && item.CreditNoteExpiryDate.Value.Day == 31)), Round(evidenceRows.Where(item => item.CreditNoteExpiryDate.HasValue && !(item.CreditNoteExpiryDate.Value.Month == 3 && item.CreditNoteExpiryDate.Value.Day == 31)).Sum(item => item.CreditNoteOpenAmount)), "Credit notes issued in Jan/Feb/Mar with six-month validity."),
            new("Expired open", evidenceRows.Count(item => item.ExpiryStatus == "Expired open"), expiredOpenCredit, "Open amount after expiry date."),
            new("Expiring soon", evidenceRows.Count(item => item.ExpiryStatus == "Expiring soon"), expiringSoonCredit, "Open amount expiring within 30 days.")
        };

        return new GoodsReturnAcceptanceReportDto(
            "Goods Return / Exchange Operational Acceptance",
            status,
            fromDate,
            toDate,
            DateTime.Now,
            criticalIssues,
            warningIssues,
            totalReturnValue,
            totalRefundValue,
            totalOpenCredit,
            expiredOpenCredit,
            expiringSoonCredit,
            exchangedValue,
            metrics,
            creditNoteBuckets,
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).ThenBy(item => item.Code).ThenBy(item => item.ReturnInvoiceNumber).Take(EvidenceLimit).ToList(),
            evidenceRows.OrderByDescending(item => item.ReturnDate).ThenBy(item => item.CustomerName).Take(EvidenceLimit).ToList(),
            CloseoutChecklist(),
            OperatorRules(),
            KnownLimitations(),
            NextModuleCandidates());
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

    private static DateTime CalculateCreditNoteExpiry(DateTime noteDate)
    {
        var date = noteDate.Date;
        if (date.Month is >= 1 and <= 3)
        {
            return date.AddMonths(6).Date;
        }

        return new DateTime(date.Year + 1, 3, 31);
    }

    private static string BuildExpiryStatus(CommercialNote? note, DateTime? expiryDate, DateTime today)
    {
        if (note is null || !expiryDate.HasValue)
        {
            return "No credit note";
        }

        var open = Math.Max(note.Amount - note.AdjustedAmount, 0);
        if (open <= AmountTolerance)
        {
            return "Fully adjusted";
        }

        if (expiryDate.Value.Date < today)
        {
            return "Expired open";
        }

        if (expiryDate.Value.Date <= today.AddDays(30))
        {
            return "Expiring soon";
        }

        return "Valid open";
    }

    private static string BuildRowStatus(
        Invoice? originalInvoice,
        bool withinPolicyDays,
        decimal refundAmount,
        int noteCount,
        int overAdjustedNotes,
        bool expiredOpen,
        int unprintedOpenNotes,
        bool expiringSoon,
        int missingBarcodeItems,
        bool hasImmediateExchange,
        decimal openCredit,
        int exchangeInvoiceCount,
        decimal exchangePaidByCredit)
    {
        if (originalInvoice is null || !withinPolicyDays || refundAmount > AmountTolerance || noteCount == 0 || overAdjustedNotes > 0 || expiredOpen)
        {
            return "Critical";
        }

        if (unprintedOpenNotes > 0 || expiringSoon || missingBarcodeItems > 0 || (!hasImmediateExchange && openCredit > AmountTolerance) || (exchangeInvoiceCount > 0 && exchangePaidByCredit <= AmountTolerance))
        {
            return "Warning";
        }

        return "Pass";
    }

    private static void AddIssueIf(List<GoodsReturnIssueDto> issues, bool condition, string severity, string code, Invoice returnInvoice, string message, decimal amount)
    {
        if (!condition)
        {
            return;
        }

        issues.Add(new GoodsReturnIssueDto(returnInvoice.Id, returnInvoice.InvoiceNumber, returnInvoice.OnDate, returnInvoice.CustomerName ?? "Walk-in Customer", returnInvoice.CustomerMobileNumber, severity, code, message, Round(amount)));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => $"₹{value:N2}";

    private static IReadOnlyList<string> CloseoutChecklist() => new[]
    {
        "Every goods return must be linked to the original sale invoice and accepted within 7 days of purchase.",
        "No cash/bank refund should be posted for product return; only exchange or store credit note is allowed.",
        "Linked customer credit note must match the approved return amount and must not be over-adjusted.",
        "Credit note expiry must follow policy: up to 31 March of the financial year, or 6 months for Jan/Feb/Mar issued notes.",
        "Open credit notes must be printed/shared with customer and reviewed before expiry.",
        "Replacement exchange invoice should consume return credit through a linked credit/store-credit payment row.",
        "Physical product inspection remains mandatory: unused, original packing, intact price tag, no tear/stain/dirty condition."
    };

    private static IReadOnlyList<string> OperatorRules() => new[]
    {
        "Do not delete a posted return or credit note to hide a mistake; create controlled correction/reversal evidence.",
        "Do not issue money refund for product return unless owner/admin records a documented exception outside normal policy.",
        "Do not accept product return without original invoice copy or verifiable digital invoice link.",
        "For Jan, Feb and Mar exchange credits, show the exact six-month validity date to the customer.",
        "Before FY close, clear or review every open credit note expiring on or before 31 March."
    };

    private static IReadOnlyList<string> KnownLimitations() => new[]
    {
        "This stage validates existing operational evidence; it does not add photo proof or tag-condition image capture for returned goods.",
        "Physical condition checks like unused item, intact tag, original packing and no stain/tear are policy/operator checks unless future image evidence is added.",
        "Credit-note expiry is computed at report time from note date and policy rules; no database expiry column is added in this stage.",
        "Owner-approved exceptional refunds are still flagged because the standard policy is no-refund exchange/credit only."
    };

    private static IReadOnlyList<string> NextModuleCandidates() => new[]
    {
        "Financial Year Closeout Dashboard — because open/expiring credit notes now need FY-close evidence.",
        "Return Product Photo Proof — optional future enhancement for price-tag/original-packing/unused-condition image capture.",
        "Customer WhatsApp Credit Note Reminder — optional reminder workflow before credit-note expiry."
    };

    private static string BuildCsv(GoodsReturnAcceptanceReportDto report)
    {
        var builder = new StringBuilder();
        void Row(params object?[] cells) => builder.AppendLine(string.Join(',', cells.Select(Escape)));

        Row("Garmetix Goods Return / Exchange Operational Acceptance");
        Row("Status", report.Status, "From", report.From.ToString("yyyy-MM-dd"), "To", report.To.ToString("yyyy-MM-dd"), "Generated", report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        Row("Critical", report.CriticalIssues, "Warnings", report.WarningIssues, "Return Value", report.TotalReturnValue, "Refund Amount", report.TotalRefundValue, "Open Credit", report.TotalOpenCredit);
        Row();
        Row("Metrics");
        Row("Label", "Count", "Amount", "Description");
        foreach (var metric in report.Metrics)
        {
            Row(metric.Label, metric.Count, metric.Amount, metric.Description);
        }

        Row();
        Row("Credit Note Validity Buckets");
        Row("Bucket", "Count", "Open Amount", "Description");
        foreach (var bucket in report.CreditNoteBuckets)
        {
            Row(bucket.Bucket, bucket.Count, bucket.OpenAmount, bucket.Description);
        }

        Row();
        Row("Issues");
        Row("Severity", "Code", "Return Invoice", "Return Date", "Customer", "Mobile", "Message", "Amount");
        foreach (var issue in report.Issues)
        {
            Row(issue.Severity, issue.Code, issue.ReturnInvoiceNumber, issue.ReturnDate.ToString("yyyy-MM-dd"), issue.CustomerName, issue.MobileNumber, issue.Message, issue.Amount);
        }

        Row();
        Row("Evidence Rows");
        Row("Return Invoice", "Return Date", "Original Invoice", "Original Date", "Days From Sale", "Policy Days", "Customer", "Mobile", "Return Amount", "Refund Amount", "Credit Note Count", "Credit Note", "Credit Note Amount", "Credit Adjusted", "Credit Open", "Credit Expiry", "Expiry Status", "Exchange Count", "Exchange Invoices", "Exchange Value", "Exchange Credit Applied", "Return Qty", "Missing Barcode Items", "Status");
        foreach (var row in report.EvidenceRows)
        {
            Row(row.ReturnInvoiceNumber, row.ReturnDate.ToString("yyyy-MM-dd"), row.OriginalInvoiceNumber, row.OriginalInvoiceDate?.ToString("yyyy-MM-dd"), row.DaysFromSale, row.PolicyDaysStatus, row.CustomerName, row.MobileNumber, row.ReturnAmount, row.RefundAmount, row.CreditNoteCount, row.CreditNoteNumber, row.CreditNoteAmount, row.CreditNoteAdjustedAmount, row.CreditNoteOpenAmount, row.CreditNoteExpiryDate?.ToString("yyyy-MM-dd"), row.ExpiryStatus, row.ExchangeInvoiceCount, row.ExchangeInvoiceNumbers, row.ExchangeInvoiceValue, row.ExchangeCreditApplied, row.ReturnQuantity, row.MissingBarcodeItemCount, row.Status);
        }

        Row();
        Row("Closeout Checklist");
        foreach (var item in report.CloseoutChecklist) Row(item);
        Row();
        Row("Operator Rules");
        foreach (var item in report.OperatorRules) Row(item);
        Row();
        Row("Known Limitations");
        foreach (var item in report.KnownLimitations) Row(item);

        return builder.ToString();
    }

    private static string Escape(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd"),
            decimal number => number.ToString("0.##"),
            _ => Convert.ToString(value) ?? string.Empty
        };

        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
    }
}

public sealed record GoodsReturnAcceptanceReportDto(
    string Title,
    string Status,
    DateTime From,
    DateTime To,
    DateTime GeneratedAt,
    int CriticalIssues,
    int WarningIssues,
    decimal TotalReturnValue,
    decimal TotalRefundValue,
    decimal TotalOpenCredit,
    decimal ExpiredOpenCredit,
    decimal ExpiringSoonCredit,
    decimal ExchangedValue,
    IReadOnlyList<GoodsReturnMetricDto> Metrics,
    IReadOnlyList<GoodsReturnCreditBucketDto> CreditNoteBuckets,
    IReadOnlyList<GoodsReturnIssueDto> Issues,
    IReadOnlyList<GoodsReturnEvidenceRowDto> EvidenceRows,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record GoodsReturnMetricDto(string Label, int Count, decimal? Amount, string Description);

public sealed record GoodsReturnCreditBucketDto(string Bucket, int Count, decimal OpenAmount, string Description);

public sealed record GoodsReturnIssueDto(
    Guid ReturnInvoiceId,
    string ReturnInvoiceNumber,
    DateTime ReturnDate,
    string CustomerName,
    string? MobileNumber,
    string Severity,
    string Code,
    string Message,
    decimal Amount);

public sealed record GoodsReturnEvidenceRowDto(
    Guid ReturnInvoiceId,
    string ReturnInvoiceNumber,
    DateTime ReturnDate,
    Guid? OriginalInvoiceId,
    string? OriginalInvoiceNumber,
    DateTime? OriginalInvoiceDate,
    int? DaysFromSale,
    string PolicyDaysStatus,
    Guid CustomerId,
    string CustomerName,
    string? MobileNumber,
    decimal ReturnAmount,
    decimal RefundAmount,
    int CreditNoteCount,
    string? CreditNoteNumber,
    decimal CreditNoteAmount,
    decimal CreditNoteAdjustedAmount,
    decimal CreditNoteOpenAmount,
    DateTime? CreditNoteExpiryDate,
    string ExpiryStatus,
    int ExchangeInvoiceCount,
    string ExchangeInvoiceNumbers,
    decimal ExchangeInvoiceValue,
    decimal ExchangeCreditApplied,
    decimal ReturnQuantity,
    int MissingBarcodeItemCount,
    string Status);
