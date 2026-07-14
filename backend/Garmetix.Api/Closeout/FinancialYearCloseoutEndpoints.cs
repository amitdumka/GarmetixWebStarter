using System.Globalization;
using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Accounting;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Closeout;

public static class FinancialYearCloseoutEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 300;

    public static RouteGroupBuilder MapFinancialYearCloseoutEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/financial-year-closeout")
            .WithTags("Financial Year Closeout")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("", GetStatusAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<FinancialYearCloseoutReportDto> GetStatusAsync(
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
        var fileName = $"garmetix-financial-year-closeout-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<FinancialYearCloseoutReportDto> BuildReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        var (fromDate, toDate, toExclusive, financialYear) = ResolveDateRange(from, to);
        var issues = new List<FinancialYearCloseoutIssueDto>();
        var sections = new List<FinancialYearCloseoutSectionDto>();
        var metrics = new List<FinancialYearCloseoutMetricDto>();
        var monthForPayroll = toDate.Year * 100 + toDate.Month;
        var monthStart = new DateTime(toDate.Year, toDate.Month, 1);
        var monthEndExclusive = monthStart.AddMonths(1);

        var storeIdsForGroup = new List<Guid>();
        if (storeGroupId.HasValue && !storeId.HasValue)
        {
            var storeQuery = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Where(item => item.StoreGroupId == storeGroupId.Value && !item.Deleted);
            if (companyId.HasValue) storeQuery = storeQuery.Where(item => item.CompanyId == companyId.Value);
            storeIdsForGroup = await storeQuery.Select(item => item.Id).ToListAsync(cancellationToken);
        }

        var salesQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        salesQuery = ApplyInvoiceScope(salesQuery, companyId, storeGroupId, storeId, storeIdsForGroup);
        var sales = await salesQuery.ToListAsync(cancellationToken);
        var activeSales = sales.Where(item => !item.ReturnInvoice).ToList();
        var saleReturns = sales.Where(item => item.ReturnInvoice).ToList();

        var purchaseQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        purchaseQuery = ApplyPurchaseScope(purchaseQuery, companyId, storeGroupId, storeId);
        var purchases = await purchaseQuery.ToListAsync(cancellationToken);

        var saleIds = sales.Select(item => item.Id).Distinct().ToList();
        var activeSaleIds = activeSales.Select(item => item.Id).Distinct().ToList();
        var purchaseIds = purchases.Select(item => item.Id).Distinct().ToList();

        var saleItems = saleIds.Count == 0
            ? new List<Garmetix.Core.Models.Inventory.InvoiceItem>()
            : await db.InvoiceItems.AsNoTracking()
                .Where(item => !item.Deleted && saleIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var purchaseItems = purchaseIds.Count == 0
            ? new List<Garmetix.Core.Models.Inventory.PurchaseInvoiceItem>()
            : await db.PurchaseInvoiceItems.AsNoTracking()
                .Where(item => !item.Deleted && purchaseIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var salePayments = activeSaleIds.Count == 0
            ? new List<Garmetix.Core.Models.Inventory.InvoicePayment>()
            : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && activeSaleIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var purchasePayments = purchaseIds.Count == 0
            ? new List<Garmetix.Core.Models.Inventory.PurchasePayment>()
            : await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && purchaseIds.Contains(item.PurchaseInvoiceId))
                .ToListAsync(cancellationToken);

        var journalQuery = WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        journalQuery = ApplyStoreBaseScope(journalQuery, companyId, storeGroupId, storeId);
        var journals = await journalQuery.ToListAsync(cancellationToken);
        var journalIds = journals.Select(item => item.Id).Distinct().ToList();
        var journalLines = journalIds.Count == 0
            ? new List<Garmetix.Core.Models.Accounting.JournalLine>()
            : await WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context)
                .Where(item => !item.Deleted && journalIds.Contains(item.JournalEntryId))
                .ToListAsync(cancellationToken);

        var stockQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context).Where(item => !item.Deleted);
        stockQuery = ApplyStoreBaseScope(stockQuery, companyId, storeGroupId, storeId);
        var negativeStocks = await stockQuery.CountAsync(item => item.PurchaseQty - item.SoldQty < 0, cancellationToken);
        var stockValue = await stockQuery.SumAsync(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice, cancellationToken);

        var customerCreditNotesQuery = WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.NoteType == NoteType.CreditNote && item.PartyType == PartyType.Customer && item.OnDate < toExclusive);
        customerCreditNotesQuery = ApplyStoreBaseScope(customerCreditNotesQuery, companyId, storeGroupId, storeId);
        var customerCreditNotes = await customerCreditNotesQuery.ToListAsync(cancellationToken);

        var vendorDebitNotesQuery = WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.NoteType == NoteType.DebitNote && item.PartyType == PartyType.Vendor && item.OnDate < toExclusive);
        vendorDebitNotesQuery = ApplyStoreBaseScope(vendorDebitNotesQuery, companyId, storeGroupId, storeId);
        var vendorDebitNotes = await vendorDebitNotesQuery.ToListAsync(cancellationToken);

        var customerDues = Round(activeSales.Sum(item => Math.Max(item.BillAmount - item.PaidAmount, 0m)));
        var vendorPayable = Round(purchases.Sum(invoice => Math.Max(invoice.BillAmount - purchasePayments.Where(item => item.PurchaseInvoiceId == invoice.Id).Sum(item => item.Amount), 0m)));
        var openCredit = Round(customerCreditNotes.Sum(item => Math.Max(item.Amount - item.AdjustedAmount, 0m)));
        var openDebit = Round(vendorDebitNotes.Sum(item => Math.Max(item.Amount - item.AdjustedAmount, 0m)));
        var expiredOpenCredit = customerCreditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance && CalculateCreditNoteExpiry(item.OnDate) < toDate.Date);
        var expiringCredit = customerCreditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance && CalculateCreditNoteExpiry(item.OnDate) >= toDate.Date && CalculateCreditNoteExpiry(item.OnDate) <= toDate.Date.AddDays(30));
        var unprintedOpenCredit = customerCreditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance && !item.Printed);
        var unprintedOpenDebit = vendorDebitNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance && !item.Printed);

        var saleItemsByInvoice = saleItems.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var purchaseItemsByInvoice = purchaseItems.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var salePaymentsByInvoice = salePayments.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var purchasePaymentsByInvoice = purchasePayments.GroupBy(item => item.PurchaseInvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var journalSourceKeys = journals
            .Where(item => item.SourceId.HasValue)
            .Select(item => $"{item.SourceType}|{item.SourceId!.Value}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var saleItemMismatch = 0;
        var saleTaxMismatch = 0;
        var salePaymentMismatch = 0;
        var salesMissingJournal = 0;
        foreach (var invoice in activeSales)
        {
            saleItemsByInvoice.TryGetValue(invoice.Id, out var rows);
            rows ??= new List<Garmetix.Core.Models.Inventory.InvoiceItem>();
            var rowTotal = Round(rows.Sum(item => item.Amount));
            var taxTotal = Round(rows.Sum(item => item.TaxAmount));
            if (rows.Count == 0 || Math.Abs(rowTotal - invoice.BillAmount) > AmountTolerance) saleItemMismatch++;
            if (Math.Abs(taxTotal - invoice.TaxAmount) > AmountTolerance) saleTaxMismatch++;
            if (salePaymentsByInvoice.TryGetValue(invoice.Id, out var payRows) && Math.Abs(Round(payRows.Sum(item => item.Amount)) - invoice.PaidAmount) > AmountTolerance) salePaymentMismatch++;
            if (!journalSourceKeys.Contains($"SaleInvoice|{invoice.Id}")) salesMissingJournal++;
        }

        var purchaseItemMismatch = 0;
        var purchaseTaxMismatch = 0;
        var purchasePaymentMismatch = 0;
        var purchasesMissingJournal = 0;
        foreach (var invoice in purchases)
        {
            purchaseItemsByInvoice.TryGetValue(invoice.Id, out var rows);
            rows ??= new List<Garmetix.Core.Models.Inventory.PurchaseInvoiceItem>();
            var rowTotal = Round(rows.Sum(item => item.Amount));
            var taxTotal = Round(rows.Sum(item => item.TaxAmount));
            if (rows.Count == 0 || Math.Abs(rowTotal - invoice.BillAmount) > AmountTolerance) purchaseItemMismatch++;
            if (Math.Abs(taxTotal - invoice.TaxAmount) > AmountTolerance) purchaseTaxMismatch++;
            if (purchasePaymentsByInvoice.TryGetValue(invoice.Id, out var payRows) && Round(payRows.Sum(item => item.Amount) - invoice.BillAmount) > AmountTolerance) purchasePaymentMismatch++;
            if (!journalSourceKeys.Contains($"PurchaseInvoice|{invoice.Id}")) purchasesMissingJournal++;
        }

        var linesByJournal = journalLines.GroupBy(item => item.JournalEntryId).ToDictionary(group => group.Key, group => group.ToList());
        var unbalancedJournals = 0;
        foreach (var journal in journals)
        {
            linesByJournal.TryGetValue(journal.Id, out var lines);
            lines ??= new List<Garmetix.Core.Models.Accounting.JournalLine>();
            if (lines.Count == 0 || Math.Abs(Round(lines.Sum(item => item.Debit) - lines.Sum(item => item.Credit))) > AmountTolerance) unbalancedJournals++;
        }

        var lockQuery = WorkspaceScope.ApplyTo(db.FinancialYearLocks.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.Active && item.FinancialYear == financialYear && item.PeriodStart <= fromDate && item.PeriodEnd >= toDate);
        if (companyId.HasValue) lockQuery = lockQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) lockQuery = lockQuery.Where(item => item.StoreId == storeId.Value);
        else if (storeGroupId.HasValue) lockQuery = lockQuery.Where(item => item.StoreGroupId == storeGroupId.Value || (item.StoreId.HasValue && storeIdsForGroup.Contains(item.StoreId.Value)));
        var locks = await lockQuery.ToListAsync(cancellationToken);
        var fullLock = locks.Any(item => item.LockAccounting && item.LockSales && item.LockPurchase && item.LockInventory && item.LockGst);

        var activeEmployeesQuery = WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context).Where(item => !item.Deleted && item.Working);
        activeEmployeesQuery = ApplyStoreBaseScope(activeEmployeesQuery, companyId, storeGroupId, storeId);
        var activeEmployees = await activeEmployeesQuery.Select(item => item.Id).ToListAsync(cancellationToken);
        var employeeCount = activeEmployees.Count;

        var monthlyRows = employeeCount == 0
            ? 0
            : await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.Year == toDate.Year && item.Month == toDate.Month && activeEmployees.Contains(item.EmployeeId))
                .CountAsync(cancellationToken);
        var unlockedMonthlyRows = employeeCount == 0
            ? 0
            : await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.Year == toDate.Year && item.Month == toDate.Month && activeEmployees.Contains(item.EmployeeId) && !item.Locked)
                .CountAsync(cancellationToken);
        var approvedReviews = employeeCount == 0
            ? 0
            : await WorkspaceScope.ApplyTo(db.AttendancePayrollReviews.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.Year == toDate.Year && item.Month == toDate.Month && activeEmployees.Contains(item.EmployeeId) && (item.ReviewStatus == "Approved" || item.ReviewStatus == "ApprovedForPayroll" || item.ReviewStatus == "Final"))
                .CountAsync(cancellationToken);
        var readyDrafts = employeeCount == 0
            ? 0
            : await WorkspaceScope.ApplyTo(db.AttendanceSalarySlipDrafts.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.Year == toDate.Year && item.Month == toDate.Month && activeEmployees.Contains(item.EmployeeId) && (item.DraftStatus == "Ready" || item.DraftStatus == "Posted" || item.PayrollPostStatus == "Posted"))
                .CountAsync(cancellationToken);
        var payslips = employeeCount == 0
            ? new List<Garmetix.Core.Models.HRM.SalaryPaySlip>()
            : await WorkspaceScope.ApplyTo(db.SalaryPaySlips.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.PayPeriodStart >= monthStart && item.PayPeriodStart < monthEndExclusive && activeEmployees.Contains(item.EmployeeId))
                .ToListAsync(cancellationToken);
        var salaryPayments = employeeCount == 0
            ? new List<Garmetix.Core.Models.HRM.SalaryPayment>()
            : await WorkspaceScope.ApplyTo(db.SalaryPayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SalaryMonth == monthForPayroll && activeEmployees.Contains(item.EmployeeId))
                .ToListAsync(cancellationToken);
        var payrollNet = Round(payslips.Sum(item => item.NetSalary));
        var payrollPaid = Round(salaryPayments.Sum(item => item.Amount));
        var payrollOutstanding = Round(Math.Max(payrollNet - payrollPaid, 0m));

        var purchaseImportQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoiceImportBatches.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.CreatedAt < toExclusive);
        purchaseImportQuery = ApplyStoreBaseScope(purchaseImportQuery, companyId, storeGroupId, storeId);
        var postedPurchaseImports = await purchaseImportQuery.CountAsync(item => item.PostedPurchaseInvoiceId.HasValue, cancellationToken);
        var untestedPostedImports = await purchaseImportQuery.CountAsync(item => item.PostedPurchaseInvoiceId.HasValue && item.AcceptanceTestedAt == null, cancellationToken);
        var correctionRequiredImports = await purchaseImportQuery.CountAsync(item => item.CorrectionStatus == "CorrectionRequired" || item.CorrectionStatus == "Required", cancellationToken);

        var vyaparImportedInvoices = activeSales.Count(item => item.Remarks != null && item.Remarks.Contains("VyaparImportBatchId="));

        var outputGst = Round(saleItems.Where(item => activeSaleIds.Contains(item.InvoiceId)).Sum(item => item.TaxAmount));
        var returnGst = Round(saleItems.Where(item => !activeSaleIds.Contains(item.InvoiceId)).Sum(item => item.TaxAmount));
        var inputGst = Round(purchaseItems.Sum(item => item.TaxAmount));
        var netGst = Round(outputGst - returnGst - inputGst);

        metrics.AddRange([
            new("Sales", activeSales.Count, Round(activeSales.Sum(item => item.BillAmount)), "Active sale invoices in selected period."),
            new("Sales returns", saleReturns.Count, Round(saleReturns.Sum(item => item.BillAmount)), "Return invoices in selected period."),
            new("Purchase inward", purchases.Count, Round(purchases.Sum(item => item.BillAmount)), "Purchase invoices/inwards in selected period."),
            new("Customer dues", activeSales.Count(item => item.BillAmount - item.PaidAmount > AmountTolerance), customerDues, "Customer receivable still open at close date."),
            new("Vendor payable", purchases.Count(item => Math.Max(item.BillAmount - (purchasePaymentsByInvoice.TryGetValue(item.Id, out var rows) ? rows.Sum(row => row.Amount) : 0m), 0m) > AmountTolerance), vendorPayable, "Supplier payable still open at close date."),
            new("Open customer credit", customerCreditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance), openCredit, "Unadjusted credit notes / store credit."),
            new("Open vendor debit", vendorDebitNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance), openDebit, "Open supplier debit notes."),
            new("Net GST snapshot", 1, netGst, "Output GST minus return GST minus input GST from item snapshots."),
            new("Stock value", negativeStocks, Round(stockValue), "Current stock cost value; count shows negative stock rows."),
            new("Payroll outstanding", salaryPayments.Count, payrollOutstanding, "Selected close month salary still unpaid.")
        ]);

        AddIssueIf(issues, saleItemMismatch > 0, "Critical", "SALE_ITEM_TOTAL_MISMATCH", $"{saleItemMismatch} sale invoice(s) have missing/mismatched item totals.", "/billing/final-qa");
        AddIssueIf(issues, saleTaxMismatch > 0, "Critical", "SALE_GST_MISMATCH", $"{saleTaxMismatch} sale invoice(s) have GST item snapshot mismatch.", "/billing/final-qa");
        AddIssueIf(issues, salePaymentMismatch > 0, "Critical", "SALE_PAYMENT_MISMATCH", $"{salePaymentMismatch} sale invoice(s) have paid amount mismatch with payment rows.", "/billing/final-qa");
        AddIssueIf(issues, salesMissingJournal > 0, "Critical", "SALE_JOURNAL_MISSING", $"{salesMissingJournal} sale invoice(s) do not have accounting journal evidence.", "/accounting-gst-validation");
        AddIssueIf(issues, purchaseItemMismatch > 0, "Critical", "PURCHASE_ITEM_TOTAL_MISMATCH", $"{purchaseItemMismatch} purchase inward(s) have missing/mismatched item totals.", "/accounting-gst-validation");
        AddIssueIf(issues, purchaseTaxMismatch > 0, "Critical", "PURCHASE_GST_MISMATCH", $"{purchaseTaxMismatch} purchase inward(s) have GST item snapshot mismatch.", "/accounting-gst-validation");
        AddIssueIf(issues, purchasePaymentMismatch > 0, "Critical", "PURCHASE_PAYMENT_OVERPAY", $"{purchasePaymentMismatch} purchase inward(s) appear overpaid by payment rows.", "/purchase/vendor-payable-reconciliation");
        AddIssueIf(issues, purchasesMissingJournal > 0, "Critical", "PURCHASE_JOURNAL_MISSING", $"{purchasesMissingJournal} purchase inward(s) do not have accounting journal evidence.", "/accounting-gst-validation");
        AddIssueIf(issues, unbalancedJournals > 0, "Critical", "JOURNAL_NOT_BALANCED", $"{unbalancedJournals} journal entry/entries are not balanced or have no lines.", "/accounting");
        AddIssueIf(issues, negativeStocks > 0, "Critical", "NEGATIVE_STOCK", $"{negativeStocks} stock row(s) are negative at closeout.", "/stock-reports");
        AddIssueIf(issues, expiredOpenCredit > 0, "Critical", "EXPIRED_OPEN_CREDIT", $"{expiredOpenCredit} open customer credit note(s) are expired.", "/goods-return-acceptance");
        AddIssueIf(issues, correctionRequiredImports > 0, "Critical", "PURCHASE_IMPORT_CORRECTION_REQUIRED", $"{correctionRequiredImports} purchase import batch(es) still require correction.", "/purchase/import-acceptance");
        AddIssueIf(issues, payrollOutstanding > AmountTolerance, "Critical", "PAYROLL_OUTSTANDING", $"Payroll outstanding for {monthStart:MMM yyyy} is {payrollOutstanding:0.##}.", "/payroll/finalization");
        AddIssueIf(issues, !fullLock, "Critical", "FY_LOCK_MISSING", "No active financial-year lock covers this period with Accounting/Sales/Purchase/Inventory/GST locked.", "/financial-year-locks");

        AddIssueIf(issues, customerDues > AmountTolerance, "Warning", "CUSTOMER_DUES_OPEN", $"Customer dues of {customerDues:0.##} remain open.", "/customers/dues-reconciliation");
        AddIssueIf(issues, vendorPayable > AmountTolerance, "Warning", "VENDOR_PAYABLE_OPEN", $"Vendor payable of {vendorPayable:0.##} remains open.", "/purchase/vendor-payable-reconciliation");
        AddIssueIf(issues, openCredit > AmountTolerance, "Warning", "CUSTOMER_CREDIT_OPEN", $"Customer credit of {openCredit:0.##} remains open.", "/goods-return-acceptance");
        AddIssueIf(issues, openDebit > AmountTolerance, "Warning", "VENDOR_DEBIT_OPEN", $"Vendor debit note balance of {openDebit:0.##} remains open.", "/purchase/vendor-payable-reconciliation");
        AddIssueIf(issues, expiringCredit > 0, "Warning", "CUSTOMER_CREDIT_EXPIRING", $"{expiringCredit} open customer credit note(s) expire within 30 days.", "/goods-return-acceptance");
        AddIssueIf(issues, unprintedOpenCredit > 0, "Warning", "OPEN_CREDIT_NOT_PRINTED", $"{unprintedOpenCredit} open customer credit note(s) are not printed/shared.", "/credit-notes");
        AddIssueIf(issues, unprintedOpenDebit > 0, "Warning", "OPEN_DEBIT_NOT_PRINTED", $"{unprintedOpenDebit} open vendor debit note(s) are not printed/shared.", "/debit-notes");
        AddIssueIf(issues, monthlyRows < employeeCount, "Warning", "PAYROLL_MONTHLY_ATTENDANCE_INCOMPLETE", $"Attendance monthly summary exists for {monthlyRows}/{employeeCount} active employee(s) for {monthStart:MMM yyyy}.", "/payroll/finalization");
        AddIssueIf(issues, unlockedMonthlyRows > 0, "Warning", "PAYROLL_ATTENDANCE_UNLOCKED", $"{unlockedMonthlyRows} attendance monthly summary row(s) are not locked.", "/payroll/finalization");
        AddIssueIf(issues, approvedReviews < employeeCount, "Warning", "PAYROLL_REVIEW_INCOMPLETE", $"Approved payroll review exists for {approvedReviews}/{employeeCount} active employee(s).", "/payroll/finalization");
        AddIssueIf(issues, readyDrafts < employeeCount, "Warning", "PAYROLL_DRAFT_INCOMPLETE", $"Ready/posted salary draft exists for {readyDrafts}/{employeeCount} active employee(s).", "/payroll/finalization");
        AddIssueIf(issues, untestedPostedImports > 0, "Warning", "PURCHASE_IMPORT_UNTESTED", $"{untestedPostedImports} posted purchase import batch(es) have not been marked tested/pass.", "/purchase/import-acceptance");

        sections.AddRange([
            BuildSection("Sales / Billing", saleItemMismatch + saleTaxMismatch + salePaymentMismatch + salesMissingJournal, customerDues > AmountTolerance ? 1 : 0, [
                new("Active invoices", activeSales.Count, Round(activeSales.Sum(item => item.BillAmount))),
                new("Vyapar imported invoices", vyaparImportedInvoices, Round(activeSales.Where(item => item.Remarks != null && item.Remarks.Contains("VyaparImportBatchId=")).Sum(item => item.BillAmount))),
                new("Payment rows", salePayments.Count, Round(salePayments.Sum(item => item.Amount)))
            ], "/billing/final-qa"),
            BuildSection("Purchase / Vendor", purchaseItemMismatch + purchaseTaxMismatch + purchasePaymentMismatch + purchasesMissingJournal, vendorPayable > AmountTolerance || openDebit > AmountTolerance ? 1 : 0, [
                new("Purchase invoices", purchases.Count, Round(purchases.Sum(item => item.BillAmount))),
                new("Purchase payments", purchasePayments.Count, Round(purchasePayments.Sum(item => item.Amount))),
                new("Posted purchase imports", postedPurchaseImports, null)
            ], "/purchase/vendor-payable-reconciliation"),
            BuildSection("GST / Accounting", unbalancedJournals + salesMissingJournal + purchasesMissingJournal, 0, [
                new("Output GST", activeSales.Count, outputGst),
                new("Sale return GST", saleReturns.Count, returnGst),
                new("Input GST", purchases.Count, inputGst),
                new("Journal entries", journals.Count, null)
            ], "/accounting-gst-validation"),
            BuildSection("Customer / Returns", expiredOpenCredit, customerDues > AmountTolerance || openCredit > AmountTolerance || expiringCredit > 0 ? 1 : 0, [
                new("Customer dues", activeSales.Count(item => item.BillAmount - item.PaidAmount > AmountTolerance), customerDues),
                new("Open credit", customerCreditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0m) > AmountTolerance), openCredit),
                new("Expired open credit", expiredOpenCredit, null)
            ], "/goods-return-acceptance"),
            BuildSection("Inventory", negativeStocks, 0, [
                new("Negative stock rows", negativeStocks, null),
                new("Current stock cost value", 1, Round(stockValue))
            ], "/stock-reports"),
            BuildSection("Payroll", payrollOutstanding > AmountTolerance ? 1 : 0, employeeCount > 0 && (monthlyRows < employeeCount || approvedReviews < employeeCount || readyDrafts < employeeCount) ? 1 : 0, [
                new("Active employees", employeeCount, null),
                new("Monthly rows", monthlyRows, null),
                new("Approved reviews", approvedReviews, null),
                new("Payroll paid", salaryPayments.Count, payrollPaid)
            ], "/payroll/finalization"),
            BuildSection("FY Lock", fullLock ? 0 : 1, 0, [
                new("Matching locks", locks.Count, null),
                new("Full lock", fullLock ? 1 : 0, null)
            ], "/financial-year-locks")
        ]);

        var criticalCount = issues.Count(item => item.Severity == "Critical");
        var warningCount = issues.Count(item => item.Severity == "Warning");
        var status = criticalCount == 0 && warningCount == 0 ? "Complete" : "Not Complete";

        var closeoutChecklist = new List<string>
        {
            "Run Accounting/GST Validation and clear all critical issues.",
            "Run Billing Final QA and confirm payment split, GST and journal evidence.",
            "Run Vendor Payable Reco and Customer Dues Reco before lock.",
            "Run Goods Return Acceptance and resolve expired/open credit notes.",
            "Run Payroll Finalization for the selected close month and post payments.",
            "Export Day Book CSV/PDF evidence for the period.",
            "Save print/PDF evidence for sale and purchase documents.",
            "Create/verify Financial Year Lock only after owner/accountant sign-off."
        };

        var operatorRules = new List<string>
        {
            "Do not hard-delete invoices, purchases, vouchers, credit notes or debit notes after period close.",
            "Use controlled reversal, revision, return or commercial note adjustment for corrections.",
            "Keep supplier proof, printed invoice evidence and Day Book export with the closeout file.",
            "Customer credit notes must be adjusted or carried with clear expiry tracking before FY lock.",
            "Vendor debit notes and payables must be reviewed before locking purchase/accounting."
        };

        var knownLimitations = new List<string>
        {
            "This dashboard is a control/evidence layer; it does not perform auto-repair or auto-post missing journals.",
            "Physical stock verification, CA GST filing confirmation and bank statement reconciliation still require human acceptance.",
            "Open dues/payables may be valid business decisions, but they remain warnings until explicitly accepted outside this dashboard.",
            "Financial year lock creation/edit remains on the Financial Year Locks page."
        };

        var nextModules = new List<string>
        {
            "Bank reconciliation / payment settlement closure",
            "Owner final acceptance dashboard",
            "Automated closeout evidence ZIP pack",
            "Role-based period unlock approval workflow"
        };

        return new FinancialYearCloseoutReportDto(
            financialYear,
            fromDate,
            toDate,
            status,
            criticalCount,
            warningCount,
            metrics,
            sections,
            issues.Take(EvidenceLimit).ToList(),
            new FinancialYearLockEvidenceDto(locks.Count, fullLock, locks.Select(item => new FinancialYearLockRowDto(item.Id, item.FinancialYear, item.PeriodStart, item.PeriodEnd, item.StoreGroupId, item.StoreId, item.LockAccounting, item.LockSales, item.LockPurchase, item.LockInventory, item.LockGst, item.LockedAt, item.LockedBy, item.LockReason)).ToList()),
            closeoutChecklist,
            operatorRules,
            knownLimitations,
            nextModules);
    }

    private static IQueryable<T> ApplyStoreBaseScope<T>(IQueryable<T> query, Guid? companyId, Guid? storeGroupId, Guid? storeId)
        where T : Garmetix.Core.Models.Base.StoreBase
    {
        if (companyId.HasValue) query = query.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        return query;
    }

    private static IQueryable<Garmetix.Core.Models.Inventory.Invoice> ApplyInvoiceScope(
        IQueryable<Garmetix.Core.Models.Inventory.Invoice> query,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        IReadOnlyCollection<Guid> storeIdsForGroup)
    {
        if (companyId.HasValue) query = query.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        else if (storeGroupId.HasValue) query = query.Where(item => storeIdsForGroup.Contains(item.StoreId));
        return query;
    }

    private static IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> ApplyPurchaseScope(
        IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> query,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId)
    {
        if (companyId.HasValue) query = query.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) query = query.Where(item => item.StoreId == storeId.Value);
        return query;
    }

    private static FinancialYearCloseoutSectionDto BuildSection(string name, int critical, int warning, IReadOnlyList<FinancialYearCloseoutMetricDto> metrics, string path)
    {
        var status = critical > 0 ? "Critical" : warning > 0 ? "Warning" : "Complete";
        return new FinancialYearCloseoutSectionDto(name, status, critical, warning, metrics, path);
    }

    private static void AddIssueIf(List<FinancialYearCloseoutIssueDto> issues, bool condition, string severity, string code, string message, string actionPath)
    {
        if (condition) issues.Add(new FinancialYearCloseoutIssueDto(severity, code, message, actionPath));
    }

    private static (DateTime From, DateTime To, DateTime ToExclusive, string FinancialYear) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var defaultFrom = today.Month >= 4 ? new DateTime(today.Year, 4, 1) : new DateTime(today.Year - 1, 4, 1);
        var defaultTo = defaultFrom.AddYears(1).AddDays(-1);
        var fromDate = (from?.Date ?? defaultFrom);
        var toDate = (to?.Date ?? defaultTo);
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);
        var fyStartYear = fromDate.Month >= 4 ? fromDate.Year : fromDate.Year - 1;
        var financialYear = $"{fyStartYear}-{(fyStartYear + 1).ToString(CultureInfo.InvariantCulture)[2..]}";
        return (fromDate, toDate, toDate.AddDays(1), financialYear);
    }

    private static DateTime CalculateCreditNoteExpiry(DateTime noteDate)
    {
        var date = noteDate.Date;
        return date.Month is >= 1 and <= 3
            ? date.AddMonths(6)
            : new DateTime(date.Month >= 4 ? date.Year + 1 : date.Year, 3, 31);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string BuildCsv(FinancialYearCloseoutReportDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Financial Year Closeout Evidence");
        csv.AppendLine($"Financial Year,{Escape(report.FinancialYear)}");
        csv.AppendLine($"From,{report.From:yyyy-MM-dd}");
        csv.AppendLine($"To,{report.To:yyyy-MM-dd}");
        csv.AppendLine($"Status,{Escape(report.Status)}");
        csv.AppendLine($"Critical Issues,{report.CriticalIssues}");
        csv.AppendLine($"Warning Issues,{report.WarningIssues}");
        csv.AppendLine();
        csv.AppendLine("Metrics");
        csv.AppendLine("Label,Count,Amount,Description");
        foreach (var metric in report.Metrics)
        {
            csv.AppendLine($"{Escape(metric.Label)},{metric.Count},{metric.Amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty},{Escape(metric.Description)}");
        }
        csv.AppendLine();
        csv.AppendLine("Section Status");
        csv.AppendLine("Section,Status,Critical,Warning,Action Path");
        foreach (var section in report.Sections)
        {
            csv.AppendLine($"{Escape(section.Name)},{Escape(section.Status)},{section.CriticalIssues},{section.WarningIssues},{Escape(section.ActionPath)}");
        }
        csv.AppendLine();
        csv.AppendLine("Issues");
        csv.AppendLine("Severity,Code,Message,Action Path");
        foreach (var issue in report.Issues)
        {
            csv.AppendLine($"{Escape(issue.Severity)},{Escape(issue.Code)},{Escape(issue.Message)},{Escape(issue.ActionPath)}");
        }
        csv.AppendLine();
        csv.AppendLine("Lock Evidence");
        csv.AppendLine("LockId,FinancialYear,PeriodStart,PeriodEnd,StoreGroupId,StoreId,Accounting,Sales,Purchase,Inventory,GST,LockedAt,LockedBy,Reason");
        foreach (var row in report.LockEvidence.Rows)
        {
            csv.AppendLine($"{row.Id},{Escape(row.FinancialYear)},{row.PeriodStart:yyyy-MM-dd},{row.PeriodEnd:yyyy-MM-dd},{row.StoreGroupId},{row.StoreId},{row.LockAccounting},{row.LockSales},{row.LockPurchase},{row.LockInventory},{row.LockGst},{row.LockedAt:yyyy-MM-dd HH:mm},{Escape(row.LockedBy)},{Escape(row.LockReason)}");
        }
        csv.AppendLine();
        csv.AppendLine("Closeout Checklist");
        foreach (var item in report.CloseoutChecklist) csv.AppendLine(Escape(item));
        csv.AppendLine();
        csv.AppendLine("Operator Rules");
        foreach (var item in report.OperatorRules) csv.AppendLine(Escape(item));
        csv.AppendLine();
        csv.AppendLine("Known Limitations");
        foreach (var item in report.KnownLimitations) csv.AppendLine(Escape(item));
        return csv.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

public sealed record FinancialYearCloseoutReportDto(
    string FinancialYear,
    DateTime From,
    DateTime To,
    string Status,
    int CriticalIssues,
    int WarningIssues,
    IReadOnlyList<FinancialYearCloseoutMetricDto> Metrics,
    IReadOnlyList<FinancialYearCloseoutSectionDto> Sections,
    IReadOnlyList<FinancialYearCloseoutIssueDto> Issues,
    FinancialYearLockEvidenceDto LockEvidence,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public sealed record FinancialYearCloseoutMetricDto(string Label, int Count, decimal? Amount, string Description = "");
public sealed record FinancialYearCloseoutSectionDto(string Name, string Status, int CriticalIssues, int WarningIssues, IReadOnlyList<FinancialYearCloseoutMetricDto> Metrics, string ActionPath);
public sealed record FinancialYearCloseoutIssueDto(string Severity, string Code, string Message, string ActionPath);
public sealed record FinancialYearLockEvidenceDto(int MatchingLockCount, bool FullyLocked, IReadOnlyList<FinancialYearLockRowDto> Rows);
public sealed record FinancialYearLockRowDto(Guid Id, string FinancialYear, DateTime PeriodStart, DateTime PeriodEnd, Guid? StoreGroupId, Guid? StoreId, bool LockAccounting, bool LockSales, bool LockPurchase, bool LockInventory, bool LockGst, DateTime? LockedAt, string? LockedBy, string? LockReason);
