using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Customers;

public static class CustomerDuesReconciliationEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 250;

    public static RouteGroupBuilder MapCustomerDuesReconciliationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers/dues-reconciliation")
            .WithTags("Customer Dues Reconciliation")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("", GetReconciliationAsync);
        group.MapGet("/evidence.csv", ExportEvidenceCsvAsync);

        return group;
    }

    private static async Task<CustomerDuesReconciliationReportDto> GetReconciliationAsync(
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
        var fileName = $"garmetix-customer-dues-credit-reconciliation-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<CustomerDuesReconciliationReportDto> BuildReportAsync(
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

        if (storeId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(item => item.StoreId == storeId.Value);
        }
        else if (storeGroupId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        }

        var invoices = await invoiceQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var invoiceIds = invoices.Select(item => item.Id).Distinct().ToList();
        var payments = invoiceIds.Count == 0
            ? new List<InvoicePayment>()
            : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && invoiceIds.Contains(item.InvoiceId))
                .ToListAsync(cancellationToken);

        var customerQuery = WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        if (companyId.HasValue)
        {
            customerQuery = customerQuery.Where(item => item.CompanyId == companyId.Value);
        }

        var customers = await customerQuery.ToListAsync(cancellationToken);

        var advanceQuery = WorkspaceScope.ApplyTo(db.CustomerAdvanceReceipts.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate < toExclusive);

        var creditNoteQuery = WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.NoteType == NoteType.CreditNote && item.PartyType == PartyType.Customer && item.OnDate < toExclusive);

        if (companyId.HasValue)
        {
            advanceQuery = advanceQuery.Where(item => item.CompanyId == companyId.Value);
            creditNoteQuery = creditNoteQuery.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeId.HasValue)
        {
            advanceQuery = advanceQuery.Where(item => item.StoreId == storeId.Value);
            creditNoteQuery = creditNoteQuery.Where(item => item.StoreId == storeId.Value);
        }
        else if (storeGroupId.HasValue)
        {
            advanceQuery = advanceQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
            creditNoteQuery = creditNoteQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        var advances = await advanceQuery.ToListAsync(cancellationToken);
        var creditNotes = await creditNoteQuery.ToListAsync(cancellationToken);

        var paymentsByInvoice = payments.GroupBy(item => item.InvoiceId).ToDictionary(group => group.Key, group => group.ToList());
        var invoicesByCustomer = invoices.GroupBy(item => item.CustomerId).ToDictionary(group => group.Key, group => group.ToList());
        var advancesByCustomer = advances.GroupBy(item => item.CustomerId).ToDictionary(group => group.Key, group => group.ToList());
        var creditNotesByCustomer = creditNotes.Where(item => item.CustomerId.HasValue).GroupBy(item => item.CustomerId!.Value).ToDictionary(group => group.Key, group => group.ToList());

        var relevantCustomerIds = new HashSet<Guid>(invoicesByCustomer.Keys);
        foreach (var id in advancesByCustomer.Keys) relevantCustomerIds.Add(id);
        foreach (var id in creditNotesByCustomer.Keys) relevantCustomerIds.Add(id);
        foreach (var id in customers.Where(item => Math.Abs(item.CreditBalance) > AmountTolerance).Select(item => item.Id)) relevantCustomerIds.Add(id);

        var issues = new List<CustomerDuesIssueDto>();
        var evidenceRows = new List<CustomerDuesEvidenceRowDto>();

        foreach (var customer in customers.Where(item => relevantCustomerIds.Contains(item.Id)).OrderBy(item => item.Name))
        {
            var customerInvoices = invoicesByCustomer.TryGetValue(customer.Id, out var invoiceRows) ? invoiceRows : new List<Invoice>();
            var customerAdvances = advancesByCustomer.TryGetValue(customer.Id, out var advanceRows) ? advanceRows : new List<CustomerAdvanceReceipt>();
            var customerCreditNotes = creditNotesByCustomer.TryGetValue(customer.Id, out var creditNoteRows) ? creditNoteRows : new List<CommercialNote>();

            var activeInvoices = customerInvoices.Where(IsActiveSaleInvoice).ToList();
            var invoiceTotal = Round(activeInvoices.Sum(item => item.BillAmount));
            var invoicePaid = Round(activeInvoices.Sum(item => item.PaidAmount));
            var invoiceDue = Round(activeInvoices.Sum(item => Math.Max(item.BillAmount - item.PaidAmount, 0)));
            var overdueInvoiceCount = activeInvoices.Count(item => Math.Max(item.BillAmount - item.PaidAmount, 0) > AmountTolerance);

            var paymentRowsTotal = Round(activeInvoices.Sum(invoice => paymentsByInvoice.TryGetValue(invoice.Id, out var rows) ? rows.Sum(row => row.Amount) : 0m));
            var paymentMismatchCount = activeInvoices.Count(invoice =>
            {
                var rows = paymentsByInvoice.TryGetValue(invoice.Id, out var paymentRows) ? paymentRows : new List<InvoicePayment>();
                return Math.Abs(invoice.PaidAmount - rows.Sum(row => row.Amount)) > AmountTolerance;
            });
            var paidStatusWithBalanceCount = activeInvoices.Count(item => item.InvoiceStatus == InvoiceStatus.Paid && Math.Max(item.BillAmount - item.PaidAmount, 0) > AmountTolerance);
            var overpaidCount = activeInvoices.Count(item => item.PaidAmount - item.BillAmount > AmountTolerance);

            var advanceOpen = Round(customerAdvances.Sum(item => Math.Max(item.AvailableAmount, 0)));
            var advanceOverAdjusted = customerAdvances.Count(item => item.AdjustedAmount - item.Amount > AmountTolerance);
            var advanceAvailableMismatch = customerAdvances.Count(item => Math.Abs(item.AvailableAmount - Math.Max(item.Amount - item.AdjustedAmount, 0)) > AmountTolerance);
            var nonCashAdvanceMissingBank = customerAdvances.Count(item => item.Amount > 0 && RequiresBankMapping(item.PaymentMode) && !item.BankAccountId.HasValue);

            var creditNoteOpen = Round(customerCreditNotes.Sum(item => Math.Max(item.Amount - item.AdjustedAmount, 0)));
            var creditNoteOverAdjusted = customerCreditNotes.Count(item => item.AdjustedAmount - item.Amount > AmountTolerance);
            var creditNoteUnprintedOpen = customerCreditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0) > AmountTolerance && !item.Printed);
            var expectedCreditBalance = Round(advanceOpen + creditNoteOpen);
            var creditBalanceDifference = Round(customer.CreditBalance - expectedCreditBalance);

            AddIssueIf(issues, paymentMismatchCount > 0, "Critical", "CUSTOMER_INVOICE_PAYMENT_MISMATCH", customer, $"{paymentMismatchCount} invoice(s) have PaidAmount different from active payment rows.", paymentMismatchCount);
            AddIssueIf(issues, paidStatusWithBalanceCount > 0, "Critical", "CUSTOMER_PAID_STATUS_WITH_BALANCE", customer, $"{paidStatusWithBalanceCount} invoice(s) are marked Paid while balance remains.", paidStatusWithBalanceCount);
            AddIssueIf(issues, overpaidCount > 0, "Critical", "CUSTOMER_OVERPAID_INVOICE", customer, $"{overpaidCount} invoice(s) are overpaid against bill amount.", overpaidCount);
            AddIssueIf(issues, advanceOverAdjusted > 0, "Critical", "CUSTOMER_ADVANCE_OVERADJUSTED", customer, $"{advanceOverAdjusted} advance receipt(s) are adjusted above receipt amount.", advanceOverAdjusted);
            AddIssueIf(issues, creditNoteOverAdjusted > 0, "Critical", "CUSTOMER_CREDIT_NOTE_OVERADJUSTED", customer, $"{creditNoteOverAdjusted} credit note(s) are adjusted above note amount.", creditNoteOverAdjusted);
            AddIssueIf(issues, Math.Abs(creditBalanceDifference) > AmountTolerance, "Critical", "CUSTOMER_CREDIT_BALANCE_MISMATCH", customer, $"Customer master credit balance {Money(customer.CreditBalance)} differs from open advances + credit notes {Money(expectedCreditBalance)}.", creditBalanceDifference);
            AddIssueIf(issues, advanceAvailableMismatch > 0, "Warning", "CUSTOMER_ADVANCE_AVAILABLE_MISMATCH", customer, $"{advanceAvailableMismatch} advance receipt(s) have available amount different from amount minus adjusted amount.", advanceAvailableMismatch);
            AddIssueIf(issues, creditNoteUnprintedOpen > 0, "Warning", "CUSTOMER_OPEN_CREDIT_NOTE_NOT_PRINTED", customer, $"{creditNoteUnprintedOpen} open credit note(s) are not marked printed/shared.", creditNoteUnprintedOpen);
            AddIssueIf(issues, nonCashAdvanceMissingBank > 0, "Warning", "CUSTOMER_ADVANCE_BANK_MAPPING_MISSING", customer, $"{nonCashAdvanceMissingBank} non-cash advance receipt(s) are missing bank/POS/UPI mapping.", nonCashAdvanceMissingBank);

            evidenceRows.Add(new CustomerDuesEvidenceRowDto(
                customer.Id,
                customer.Name,
                customer.MobileNumber,
                activeInvoices.Count,
                overdueInvoiceCount,
                invoiceTotal,
                invoicePaid,
                paymentRowsTotal,
                invoiceDue,
                customer.CreditBalance,
                advanceOpen,
                creditNoteOpen,
                expectedCreditBalance,
                creditBalanceDifference,
                customerAdvances.Count,
                customerCreditNotes.Count,
                paymentMismatchCount,
                paidStatusWithBalanceCount,
                overpaidCount,
                advanceOverAdjusted,
                creditNoteOverAdjusted,
                nonCashAdvanceMissingBank,
                BuildCustomerStatus(paymentMismatchCount, paidStatusWithBalanceCount, overpaidCount, advanceOverAdjusted, creditNoteOverAdjusted, creditBalanceDifference, advanceAvailableMismatch, creditNoteUnprintedOpen, nonCashAdvanceMissingBank)));
        }

        var criticalIssues = issues.Count(item => item.Severity == "Critical");
        var warningIssues = issues.Count(item => item.Severity == "Warning");
        var status = criticalIssues == 0 && warningIssues == 0 ? "Complete" : "Not Complete";
        var totalDue = Round(evidenceRows.Sum(item => item.InvoiceDue));
        var totalMasterCredit = Round(evidenceRows.Sum(item => item.CustomerMasterCreditBalance));
        var totalExpectedCredit = Round(evidenceRows.Sum(item => item.ExpectedCreditBalance));

        var metrics = new List<CustomerDuesMetricDto>
        {
            new("Customers checked", evidenceRows.Count, null, "Customers with invoice, advance, credit note, or non-zero credit balance evidence."),
            new("Open due amount", evidenceRows.Sum(item => item.OpenInvoiceCount), totalDue, "Active sale invoices with customer balance due."),
            new("Master credit balance", evidenceRows.Count(item => Math.Abs(item.CustomerMasterCreditBalance) > AmountTolerance), totalMasterCredit, "Current customer master credit balance."),
            new("Expected credit", evidenceRows.Count(item => Math.Abs(item.ExpectedCreditBalance) > AmountTolerance), totalExpectedCredit, "Open advances plus open customer credit notes."),
            new("Critical issues", criticalIssues, null, "Must be cleared before customer receivable closeout."),
            new("Warnings", warningIssues, null, "Review before final sign-off.")
        };

        var creditSources = new List<CustomerCreditSourceDto>
        {
            new("Advance receipts", advances.Count, Round(advances.Sum(item => item.Amount)), Round(advances.Sum(item => item.AdjustedAmount)), Round(advances.Sum(item => item.AvailableAmount)), advances.Count(item => item.Amount > 0 && RequiresBankMapping(item.PaymentMode) && !item.BankAccountId.HasValue)),
            new("Credit notes", creditNotes.Count, Round(creditNotes.Sum(item => item.Amount)), Round(creditNotes.Sum(item => item.AdjustedAmount)), Round(creditNotes.Sum(item => Math.Max(item.Amount - item.AdjustedAmount, 0))), creditNotes.Count(item => Math.Max(item.Amount - item.AdjustedAmount, 0) > AmountTolerance && !item.Printed)),
            new("Customer master balance", evidenceRows.Count(item => Math.Abs(item.CustomerMasterCreditBalance) > AmountTolerance), totalMasterCredit, 0, totalMasterCredit, evidenceRows.Count(item => Math.Abs(item.CreditBalanceDifference) > AmountTolerance))
        };

        return new CustomerDuesReconciliationReportDto(
            status,
            fromDate,
            toDate,
            companyId,
            storeGroupId,
            storeId,
            criticalIssues,
            warningIssues,
            totalDue,
            totalMasterCredit,
            totalExpectedCredit,
            metrics,
            creditSources,
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).ThenBy(item => item.CustomerName).Take(EvidenceLimit).ToList(),
            evidenceRows.OrderByDescending(item => Math.Abs(item.CreditBalanceDifference)).ThenByDescending(item => item.InvoiceDue).Take(EvidenceLimit).ToList(),
            CloseoutChecklist(status),
            OperatorRules(),
            KnownLimitations(),
            NextModuleCandidates());
    }

    private static bool IsActiveSaleInvoice(Invoice invoice)
    {
        return invoice.InvoiceStatus != InvoiceStatus.Cancelled && !invoice.ReturnInvoice && invoice.InvoiceType != InvoiceType.Return;
    }

    private static string BuildCustomerStatus(
        int paymentMismatchCount,
        int paidStatusWithBalanceCount,
        int overpaidCount,
        int advanceOverAdjusted,
        int creditNoteOverAdjusted,
        decimal creditBalanceDifference,
        int advanceAvailableMismatch,
        int creditNoteUnprintedOpen,
        int nonCashAdvanceMissingBank)
    {
        if (paymentMismatchCount > 0 || paidStatusWithBalanceCount > 0 || overpaidCount > 0 || advanceOverAdjusted > 0 || creditNoteOverAdjusted > 0 || Math.Abs(creditBalanceDifference) > AmountTolerance)
        {
            return "Critical";
        }

        if (advanceAvailableMismatch > 0 || creditNoteUnprintedOpen > 0 || nonCashAdvanceMissingBank > 0)
        {
            return "Warning";
        }

        return "Pass";
    }

    private static void AddIssueIf(List<CustomerDuesIssueDto> issues, bool condition, string severity, string code, Customer customer, string message, decimal? amount)
    {
        if (!condition) return;
        issues.Add(new CustomerDuesIssueDto(severity, code, customer.Id, customer.Name, customer.MobileNumber, message, amount));
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

    private static bool RequiresBankMapping(PaymentMode paymentMode)
    {
        return paymentMode is PaymentMode.Card or PaymentMode.UPI or PaymentMode.Wallets or PaymentMode.IMPS or PaymentMode.RTGS or PaymentMode.NEFT or PaymentMode.Cheque or PaymentMode.DemandDraft;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) => $"₹{value:N2}";

    private static IReadOnlyList<string> CloseoutChecklist(string status)
    {
        var lines = new List<string>
        {
            "Run reconciliation for the exact accounting/GST period before month close.",
            "Clear every customer invoice payment mismatch before accepting receivable balance.",
            "Confirm Paid invoices have zero balance and no invoice is overpaid except approved advance/credit handling.",
            "Confirm customer master credit balance equals open advance receipts plus open credit notes.",
            "Print/share open credit notes or record controlled operator reason before final sign-off.",
            "Map every non-cash advance receipt to bank/POS/UPI ledger evidence.",
            "Export CSV and attach it with Day Book / Billing Final QA evidence."
        };

        if (status != "Complete")
        {
            lines.Insert(0, "Status is Not Complete. Do not close customer dues/credit until blockers are corrected.");
        }

        return lines;
    }

    private static IReadOnlyList<string> OperatorRules() => new[]
    {
        "Do not manually overwrite customer CreditBalance to hide differences; use advance receipt, credit note, sale settlement or controlled repair.",
        "No cash refund should be posted for goods return unless management adds a separate approved refund workflow.",
        "Credit notes issued for exchange policy must remain traceable to the original sale return/exchange proof.",
        "Non-cash advances must carry bank/POS/UPI mapping before day closing and GST/accounting review.",
        "Customer dues should be collected through receipt/payment rows, not by editing old posted invoices."
    };

    private static IReadOnlyList<string> KnownLimitations() => new[]
    {
        "This is a validation and evidence layer; it does not automatically repair balances.",
        "The report checks current open advance/credit-note balances up to the report end date, while invoices are period-filtered.",
        "Manual legacy data may require Data Consistency Repair before the status becomes Complete.",
        "Physical credit note sharing/printing is represented by the Printed flag where available."
    };

    private static IReadOnlyList<string> NextModuleCandidates() => new[]
    {
        "Vendor Payable / Purchase Settlement final reconciliation",
        "Goods return/exchange operational acceptance with credit-note expiry tracking",
        "Commercial Summary naming and CRM/Loyalty UX polish",
        "Financial year closeout dashboard"
    };

    private static string BuildCsv(CustomerDuesReconciliationReportDto report)
    {
        var sb = new StringBuilder();
        void Row(params object?[] values) => sb.AppendLine(string.Join(',', values.Select(Csv)));

        Row("Garmetix Customer Dues / Credit Balance Reconciliation");
        Row("Status", report.Status, "From", report.From.ToString("yyyy-MM-dd"), "To", report.To.ToString("yyyy-MM-dd"));
        Row("Critical", report.CriticalIssues, "Warnings", report.WarningIssues, "Total Due", report.TotalDue, "Master Credit", report.TotalMasterCreditBalance, "Expected Credit", report.TotalExpectedCreditBalance);
        Row();
        Row("Metrics");
        Row("Label", "Count", "Amount", "Description");
        foreach (var metric in report.Metrics)
        {
            Row(metric.Label, metric.Count, metric.Amount, metric.Description);
        }

        Row();
        Row("Credit Sources");
        Row("Source", "Rows", "Amount", "Adjusted", "Available", "Issues");
        foreach (var source in report.CreditSources)
        {
            Row(source.Source, source.RowCount, source.Amount, source.AdjustedAmount, source.AvailableAmount, source.IssueCount);
        }

        Row();
        Row("Issues");
        Row("Severity", "Code", "Customer", "Mobile", "Message", "Amount");
        foreach (var issue in report.Issues)
        {
            Row(issue.Severity, issue.Code, issue.CustomerName, issue.MobileNumber, issue.Message, issue.Amount);
        }

        Row();
        Row("Customer Evidence");
        Row("Customer", "Mobile", "Invoices", "Open Invoices", "Invoice Total", "Invoice Paid", "Payment Rows", "Invoice Due", "Master Credit", "Advance Open", "Credit Note Open", "Expected Credit", "Difference", "Advance Rows", "Credit Note Rows", "Payment Mismatch", "Paid With Balance", "Overpaid", "Advance Overadjusted", "Credit Note Overadjusted", "Noncash Advance Missing Bank", "Status");
        foreach (var row in report.EvidenceRows)
        {
            Row(row.CustomerName, row.MobileNumber, row.InvoiceCount, row.OpenInvoiceCount, row.InvoiceTotal, row.InvoicePaid, row.PaymentRowsTotal, row.InvoiceDue, row.CustomerMasterCreditBalance, row.AdvanceOpen, row.CreditNoteOpen, row.ExpectedCreditBalance, row.CreditBalanceDifference, row.AdvanceRows, row.CreditNoteRows, row.PaymentMismatchCount, row.PaidStatusWithBalanceCount, row.OverpaidCount, row.AdvanceOverAdjustedCount, row.CreditNoteOverAdjustedCount, row.NonCashAdvanceMissingBankCount, row.Status);
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

public record CustomerDuesReconciliationReportDto(
    string Status,
    DateTime From,
    DateTime To,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    int CriticalIssues,
    int WarningIssues,
    decimal TotalDue,
    decimal TotalMasterCreditBalance,
    decimal TotalExpectedCreditBalance,
    IReadOnlyList<CustomerDuesMetricDto> Metrics,
    IReadOnlyList<CustomerCreditSourceDto> CreditSources,
    IReadOnlyList<CustomerDuesIssueDto> Issues,
    IReadOnlyList<CustomerDuesEvidenceRowDto> EvidenceRows,
    IReadOnlyList<string> CloseoutChecklist,
    IReadOnlyList<string> OperatorRules,
    IReadOnlyList<string> KnownLimitations,
    IReadOnlyList<string> NextModuleCandidates);

public record CustomerDuesMetricDto(string Label, int Count, decimal? Amount, string Description);
public record CustomerCreditSourceDto(string Source, int RowCount, decimal Amount, decimal AdjustedAmount, decimal AvailableAmount, int IssueCount);
public record CustomerDuesIssueDto(string Severity, string Code, Guid CustomerId, string CustomerName, string? MobileNumber, string Message, decimal? Amount);
public record CustomerDuesEvidenceRowDto(
    Guid CustomerId,
    string CustomerName,
    string? MobileNumber,
    int InvoiceCount,
    int OpenInvoiceCount,
    decimal InvoiceTotal,
    decimal InvoicePaid,
    decimal PaymentRowsTotal,
    decimal InvoiceDue,
    decimal CustomerMasterCreditBalance,
    decimal AdvanceOpen,
    decimal CreditNoteOpen,
    decimal ExpectedCreditBalance,
    decimal CreditBalanceDifference,
    int AdvanceRows,
    int CreditNoteRows,
    int PaymentMismatchCount,
    int PaidStatusWithBalanceCount,
    int OverpaidCount,
    int AdvanceOverAdjustedCount,
    int CreditNoteOverAdjustedCount,
    int NonCashAdvanceMissingBankCount,
    string Status);
