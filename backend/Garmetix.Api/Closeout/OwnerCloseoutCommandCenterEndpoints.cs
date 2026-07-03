using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Closeout;

public static class OwnerCloseoutCommandCenterEndpoints
{
    private const int EvidenceLimit = 300;

    public static RouteGroupBuilder MapOwnerCloseoutCommandCenterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/owner-closeout-command-center")
            .WithTags("Owner Closeout Command Center")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("", GetCommandCenterAsync);
        group.MapGet("/evidence.csv", ExportCsvAsync);
        return group;
    }

    private static async Task<OwnerCloseoutCommandCenterDto> GetCommandCenterAsync(
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

    private static async Task<IResult> ExportCsvAsync(
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
        var fileName = $"garmetix-owner-closeout-command-center-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<OwnerCloseoutCommandCenterDto> BuildReportAsync(
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
        var issues = new List<OwnerCloseoutIssueDto>();
        var modules = new List<OwnerCloseoutModuleDto>();

        var salesQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return && item.InvoiceStatus != InvoiceStatus.Cancelled && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) salesQuery = salesQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) salesQuery = salesQuery.Where(item => item.StoreId == storeId.Value);
        if (storeGroupId.HasValue) salesQuery = salesQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        var sales = await salesQuery.ToListAsync(cancellationToken);
        var saleIds = sales.Select(item => item.Id).ToList();
        var saleItems = saleIds.Count == 0 ? new List<Garmetix.Core.Models.Inventory.InvoiceItem>() : await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context).Where(item => !item.Deleted && saleIds.Contains(item.InvoiceId)).ToListAsync(cancellationToken);
        var saleMovements = saleIds.Count == 0 ? new List<Garmetix.Core.Models.Inventory.StockMovement>() : await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context).Where(item => !item.Deleted && item.SourceId.HasValue && saleIds.Contains(item.SourceId.Value) && item.QuantityOut > 0).ToListAsync(cancellationToken);
        var saleRevenue = saleItems.Sum(item => item.Amount - item.TaxAmount);
        var saleCost = saleMovements.Sum(item => item.CostPrice * item.QuantityOut);
        var grossProfit = saleRevenue - saleCost;
        var missingCostItems = saleItems.Count(item => !saleMovements.Any(move => move.SourceId == item.InvoiceId && move.ProductId == item.ProductId));
        if (missingCostItems > 0) issues.Add(new("Critical", "Profit cost evidence missing", $"{missingCostItems} sale item(s) do not have linked stock-out cost evidence.", "/reports/profit-loss"));
        modules.Add(new("Profit/Loss", missingCostItems == 0 ? "Complete" : "Not Complete", saleRevenue, grossProfit, missingCostItems, "Gross profit based on stock-out movement cost.", "/reports/profit-loss"));

        var stocksQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context).Where(item => !item.Deleted);
        if (companyId.HasValue) stocksQuery = stocksQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) stocksQuery = stocksQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) stocksQuery = stocksQuery.Where(item => item.StoreId == storeId.Value);
        var stocks = await stocksQuery.ToListAsync(cancellationToken);
        var negativeStock = stocks.Count(item => item.PurchaseQty - item.SoldQty < 0);
        var missingCostStock = stocks.Count(item => item.PurchaseQty - item.SoldQty > 0 && item.CostPrice <= 0);
        var stockCostValue = stocks.Sum(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice);
        if (negativeStock > 0) issues.Add(new("Critical", "Negative stock", $"{negativeStock} stock row(s) are negative.", "/inventory/stock-valuation-closure"));
        if (missingCostStock > 0) issues.Add(new("Critical", "Stock cost missing", $"{missingCostStock} stock row(s) have quantity but zero cost.", "/inventory/stock-valuation-closure"));
        modules.Add(new("Stock Valuation", negativeStock == 0 && missingCostStock == 0 ? "Complete" : "Not Complete", stockCostValue, null, negativeStock + missingCostStock, "Current stock valued at cost.", "/inventory/stock-valuation-closure"));

        var customerDue = sales.Where(item => item.InvoiceStatus != InvoiceStatus.Paid).Sum(item => item.BillAmount - item.PaidAmount);
        if (customerDue > 1) issues.Add(new("Warning", "Customer dues open", $"Customer dues in selected range are {customerDue:0.00}.", "/customers/dues-reconciliation"));
        modules.Add(new("Customer Dues", customerDue <= 1 ? "Complete" : "Review", customerDue, null, customerDue > 1 ? 1 : 0, "Outstanding customer receivable.", "/customers/dues-reconciliation"));

        var purchaseQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return && item.InvoiceStatus != InvoiceStatus.Cancelled && item.InwardDate >= fromDate && item.InwardDate < toExclusive);
        if (companyId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.StoreId == storeId.Value);
        var purchases = await purchaseQuery.ToListAsync(cancellationToken);
        var purchaseIds = purchases.Select(item => item.Id).ToList();
        var purchasePaid = purchaseIds.Count == 0 ? 0 : await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context).Where(item => !item.Deleted && purchaseIds.Contains(item.PurchaseInvoiceId)).SumAsync(item => item.Amount, cancellationToken);
        var purchasePayable = purchases.Sum(item => item.BillAmount) - purchasePaid;
        if (purchasePayable > 1) issues.Add(new("Warning", "Vendor payable open", $"Vendor payable in selected range is {purchasePayable:0.00}.", "/purchase/vendor-payable-reconciliation"));
        modules.Add(new("Vendor Payable", purchasePayable <= 1 ? "Complete" : "Review", purchasePayable, null, purchasePayable > 1 ? 1 : 0, "Outstanding vendor payable.", "/purchase/vendor-payable-reconciliation"));

        var openCreditNotes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.NoteType == NoteType.CreditNote && !item.IsAdjusted && item.OnDate < toExclusive)
            .CountAsync(cancellationToken);
        var openDebitNotes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.NoteType == NoteType.DebitNote && !item.IsAdjusted && item.OnDate < toExclusive)
            .CountAsync(cancellationToken);
        if (openCreditNotes > 0) issues.Add(new("Warning", "Open customer credit notes", $"{openCreditNotes} open customer credit note(s) exist.", "/goods-return-acceptance"));
        if (openDebitNotes > 0) issues.Add(new("Warning", "Open vendor debit notes", $"{openDebitNotes} open vendor debit note(s) exist.", "/purchase/vendor-payable-reconciliation"));
        modules.Add(new("Return / Note Control", openCreditNotes == 0 && openDebitNotes == 0 ? "Complete" : "Review", openCreditNotes + openDebitNotes, null, openCreditNotes + openDebitNotes, "Open credit/debit note count.", "/goods-return-acceptance"));

        var unreconciledBankTransactions = await WorkspaceScope.ApplyTo(db.BankTransactions.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && !item.Reconciled)
            .CountAsync(cancellationToken);
        var unmatchedStatementLines = await WorkspaceScope.ApplyTo(db.BankStatementLines.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && !item.Reconciled)
            .CountAsync(cancellationToken);
        if (unreconciledBankTransactions + unmatchedStatementLines > 0) issues.Add(new("Critical", "Bank settlement pending", $"{unreconciledBankTransactions} bank transaction(s) and {unmatchedStatementLines} statement line(s) are not reconciled/matched.", "/bank-reconciliation-closure"));
        modules.Add(new("Bank Reconciliation", unreconciledBankTransactions + unmatchedStatementLines == 0 ? "Complete" : "Not Complete", unreconciledBankTransactions + unmatchedStatementLines, null, unreconciledBankTransactions + unmatchedStatementLines, "Unreconciled bank evidence count.", "/bank-reconciliation-closure"));

        var journalQuery = WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context).Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) journalQuery = journalQuery.Where(item => item.CompanyId == companyId.Value);
        var journals = await journalQuery.ToListAsync(cancellationToken);
        var journalIds = journals.Select(item => item.Id).ToList();
        var journalLines = journalIds.Count == 0 ? new List<Garmetix.Core.Models.Accounting.JournalLine>() : await WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context).Where(item => !item.Deleted && journalIds.Contains(item.JournalEntryId)).ToListAsync(cancellationToken);
        var journalDebit = journalLines.Sum(item => item.Debit);
        var journalCredit = journalLines.Sum(item => item.Credit);
        if (Math.Abs(journalDebit - journalCredit) > 1) issues.Add(new("Critical", "Journal imbalance", $"Journal debit and credit differ by {Math.Abs(journalDebit - journalCredit):0.00}.", "/accounting-gst-validation"));
        modules.Add(new("Accounting Journal", Math.Abs(journalDebit - journalCredit) <= 1 ? "Complete" : "Not Complete", journalDebit, journalCredit, Math.Abs(journalDebit - journalCredit) > 1 ? 1 : 0, "Debit/credit balance in selected range.", "/accounting-gst-validation"));

        var status = issues.Any(item => item.Severity == "Critical") ? "Not Complete" : "Complete";
        var metrics = new List<OwnerCloseoutMetricDto>
        {
            new("Sales ex GST", null, saleRevenue, "Tax-exclusive revenue."),
            new("Gross profit", null, grossProfit, "Revenue ex GST minus stock cost."),
            new("Stock cost value", null, stockCostValue, "Current stock master cost value."),
            new("Customer dues", null, customerDue, "Open customer receivable."),
            new("Vendor payable", null, purchasePayable, "Open vendor payable."),
            new("Critical issues", issues.Count(item => item.Severity == "Critical"), null, "Blocks closeout.")
        };

        return new OwnerCloseoutCommandCenterDto(
            status,
            fromDate,
            toDate,
            metrics,
            issues.Count(item => item.Severity == "Critical"),
            issues.Count(item => item.Severity == "Warning"),
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).Take(EvidenceLimit).ToList(),
            modules,
            new List<string>
            {
                "Run Profit/Loss and clear missing stock cost evidence.",
                "Run Stock Valuation and clear negative/zero-cost stock.",
                "Run Bank Reconciliation closure and clear unmatched settlements.",
                "Review customer dues, vendor payables and open commercial notes.",
                "Export this CSV before period lock and keep with FY Closeout evidence."
            },
            new List<string>
            {
                "This is an owner/accountant command center. It summarizes closure evidence and does not post entries.",
                "It uses current read-only data from existing modules; module-specific pages remain the place for corrections.",
                "Gross profit is estimated from sale item revenue and stock movement cost, not from full expense allocation."
            },
            new List<string> { "Financial Year Closeout", "Production Go-Live Acceptance", "Backup Restore Drill" });
    }

    private static string BuildCsv(OwnerCloseoutCommandCenterDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Section,Status,From,To,Critical,Warning");
        csv.AppendLine($"Summary,{Csv(report.Status)},{report.From:yyyy-MM-dd},{report.To:yyyy-MM-dd},{report.CriticalIssues},{report.WarningIssues}");
        csv.AppendLine();
        csv.AppendLine("Metric,Count,Amount,Description");
        foreach (var metric in report.Metrics) csv.AppendLine($"{Csv(metric.Label)},{metric.Count},{metric.Amount},{Csv(metric.Description)}");
        csv.AppendLine();
        csv.AppendLine("Module,Status,PrimaryAmount,SecondaryAmount,IssueCount,Description,ActionPath");
        foreach (var module in report.Modules) csv.AppendLine($"{Csv(module.Module)},{Csv(module.Status)},{module.PrimaryAmount},{module.SecondaryAmount},{module.IssueCount},{Csv(module.Description)},{Csv(module.ActionPath)}");
        csv.AppendLine();
        csv.AppendLine("Severity,Title,Message,ActionPath");
        foreach (var issue in report.Issues) csv.AppendLine($"{Csv(issue.Severity)},{Csv(issue.Title)},{Csv(issue.Message)},{Csv(issue.ActionPath)}");
        return csv.ToString();
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toDate = (to ?? today).Date;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);
        return (fromDate, toDate, toDate.AddDays(1));
    }

    private static string Csv(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}

public sealed record OwnerCloseoutCommandCenterDto(string Status, DateTime From, DateTime To, List<OwnerCloseoutMetricDto> Metrics, int CriticalIssues, int WarningIssues, List<OwnerCloseoutIssueDto> Issues, List<OwnerCloseoutModuleDto> Modules, List<string> CloseoutChecklist, List<string> KnownLimitations, List<string> NextModuleCandidates);
public sealed record OwnerCloseoutMetricDto(string Label, int? Count, decimal? Amount, string Description);
public sealed record OwnerCloseoutIssueDto(string Severity, string Title, string Message, string ActionPath);
public sealed record OwnerCloseoutModuleDto(string Module, string Status, decimal? PrimaryAmount, decimal? SecondaryAmount, int IssueCount, string Description, string ActionPath);
