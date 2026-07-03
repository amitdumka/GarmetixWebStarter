using System.Net;
using System.Text;
using Garmetix.Api.AppInfo;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Production;

public static class ProductionGoLiveMasterAcceptanceEndpoints
{
    private const int EvidenceLimit = 500;

    public static RouteGroupBuilder MapProductionGoLiveMasterAcceptanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/production-go-live/master-acceptance")
            .WithTags("Production Go-Live Master Acceptance")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", GetAsync);
        group.MapGet("/evidence.csv", ExportCsvAsync);
        return group;
    }

    private static async Task<ProductionGoLiveMasterAcceptanceDto> GetAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
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
        var report = await BuildAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
        var csv = BuildCsv(report);
        var fileName = $"garmetix-production-go-live-master-acceptance-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<ProductionGoLiveMasterAcceptanceDto> BuildAsync(
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
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var checks = new List<ProductionGoLiveCheckDto>();
        var issues = new List<ProductionGoLiveIssueDto>();

        var companyQuery = WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context).Where(item => !item.Deleted);
        if (companyId.HasValue) companyQuery = companyQuery.Where(item => item.Id == companyId.Value);
        var companyCount = await companyQuery.CountAsync(cancellationToken);

        var storeQuery = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context).Where(item => !item.Deleted);
        if (companyId.HasValue) storeQuery = storeQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) storeQuery = storeQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) storeQuery = storeQuery.Where(item => item.Id == storeId.Value);
        var storeCount = await storeQuery.CountAsync(cancellationToken);

        var activeUsers = await db.Users.AsNoTracking().CountAsync(item => item.IsActive, cancellationToken);
        var adminUsers = await db.Users.AsNoTracking().CountAsync(item => item.IsActive && (item.Admin || item.IsSuperAdmin || item.Role == LoginRole.Admin || item.UserType == UserType.Owner), cancellationToken);
        AddCheck(checks, issues, "Master setup", companyCount > 0 && storeCount > 0 && activeUsers > 0 && adminUsers > 0, companyCount == 0 || storeCount == 0 ? "Critical" : "Warning", $"Companies {companyCount}, stores {storeCount}, active users {activeUsers}, admin/owner users {adminUsers}.", "/setup");

        var salesQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return && item.InvoiceStatus != InvoiceStatus.Cancelled && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) salesQuery = salesQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) salesQuery = salesQuery.Where(item => item.StoreId == storeId.Value);
        if (storeGroupId.HasValue) salesQuery = salesQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        var sales = await salesQuery.Select(item => new { item.Id, item.BillAmount, item.PaidAmount, item.InvoiceStatus }).ToListAsync(cancellationToken);
        var saleIds = sales.Select(item => item.Id).ToList();
        var saleItems = saleIds.Count == 0 ? 0 : await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context).CountAsync(item => !item.Deleted && saleIds.Contains(item.InvoiceId), cancellationToken);
        var salePaymentRows = saleIds.Count == 0 ? 0 : await WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context).CountAsync(item => !item.Deleted && saleIds.Contains(item.InvoiceId), cancellationToken);
        var saleStockOut = saleIds.Count == 0 ? 0 : await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.SourceId.HasValue && saleIds.Contains(item.SourceId.Value) && item.QuantityOut > 0, cancellationToken);
        var saleMismatch = sales.Count(item => Math.Abs(item.BillAmount - item.PaidAmount) > 1 && item.InvoiceStatus == InvoiceStatus.Paid);
        AddCheck(checks, issues, "Sales billing acceptance", sales.Count > 0 && saleItems > 0 && saleMismatch == 0, sales.Count == 0 ? "Warning" : "Critical", $"Sales {sales.Count}, item rows {saleItems}, payment rows {salePaymentRows}, stock-out rows {saleStockOut}, paid-status mismatches {saleMismatch}.", "/billing/final-qa");

        var purchaseQuery = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return && item.InvoiceStatus != InvoiceStatus.Cancelled && item.InwardDate >= fromDate && item.InwardDate < toExclusive);
        if (companyId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) purchaseQuery = purchaseQuery.Where(item => item.StoreId == storeId.Value);
        var purchases = await purchaseQuery.Select(item => new { item.Id, item.BillAmount, item.InvoiceStatus }).ToListAsync(cancellationToken);
        var purchaseIds = purchases.Select(item => item.Id).ToList();
        var purchaseItems = purchaseIds.Count == 0 ? 0 : await WorkspaceScope.ApplyTo(db.PurchaseInvoiceItems.AsNoTracking(), context).CountAsync(item => !item.Deleted && purchaseIds.Contains(item.InvoiceId), cancellationToken);
        var purchasePaymentsForPeriod = purchaseIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
                .Where(item => !item.Deleted && purchaseIds.Contains(item.PurchaseInvoiceId))
                .GroupBy(item => item.PurchaseInvoiceId)
                .Select(item => new { PurchaseInvoiceId = item.Key, Amount = item.Sum(payment => payment.Amount) })
                .ToDictionaryAsync(item => item.PurchaseInvoiceId, item => item.Amount, cancellationToken);
        var purchasePaymentRows = purchaseIds.Count == 0 ? 0 : await WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context).CountAsync(item => !item.Deleted && purchaseIds.Contains(item.PurchaseInvoiceId), cancellationToken);
        var purchaseMismatch = purchases.Count(item => Math.Abs(item.BillAmount - (purchasePaymentsForPeriod.TryGetValue(item.Id, out var paidAmount) ? paidAmount : 0m)) > 1 && item.InvoiceStatus == InvoiceStatus.Paid);
        AddCheck(checks, issues, "Purchase and vendor acceptance", purchases.Count > 0 && purchaseItems > 0 && purchaseMismatch == 0, purchases.Count == 0 ? "Warning" : "Critical", $"Purchases {purchases.Count}, item rows {purchaseItems}, payment rows {purchasePaymentRows}, paid-status mismatches {purchaseMismatch}.", "/purchase/vendor-payable-reconciliation");

        var stockQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context).Where(item => !item.Deleted);
        if (companyId.HasValue) stockQuery = stockQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) stockQuery = stockQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) stockQuery = stockQuery.Where(item => item.StoreId == storeId.Value);
        var stocks = await stockQuery.Select(item => new { item.PurchaseQty, item.SoldQty, item.CostPrice }).ToListAsync(cancellationToken);
        var negativeStocks = stocks.Count(item => item.PurchaseQty - item.SoldQty < 0);
        var zeroCostStocks = stocks.Count(item => item.PurchaseQty - item.SoldQty > 0 && item.CostPrice <= 0);
        AddCheck(checks, issues, "Stock valuation acceptance", stocks.Count > 0 && negativeStocks == 0 && zeroCostStocks == 0, negativeStocks > 0 ? "Critical" : "Warning", $"Stock rows {stocks.Count}, negative stock {negativeStocks}, zero-cost stock {zeroCostStocks}.", "/inventory/stock-valuation-closure");

        var journalQuery = WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context).Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) journalQuery = journalQuery.Where(item => item.CompanyId == companyId.Value);
        var journalIds = await journalQuery.Select(item => item.Id).ToListAsync(cancellationToken);
        var journalLines = journalIds.Count == 0 ? new List<JournalLineProjection>() : await WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context)
            .Where(item => !item.Deleted && journalIds.Contains(item.JournalEntryId))
            .Select(item => new JournalLineProjection(item.Debit, item.Credit))
            .ToListAsync(cancellationToken);
        var debit = journalLines.Sum(item => item.Debit);
        var credit = journalLines.Sum(item => item.Credit);
        AddCheck(checks, issues, "Accounting/GST acceptance", journalIds.Count > 0 && Math.Abs(debit - credit) <= 1, journalIds.Count == 0 ? "Warning" : "Critical", $"Journal entries {journalIds.Count}, debit {debit:0.00}, credit {credit:0.00}, difference {Math.Abs(debit - credit):0.00}.", "/accounting-gst-validation");

        var unreconciledBankTransactions = await WorkspaceScope.ApplyTo(db.BankTransactions.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && !item.Reconciled, cancellationToken);
        var unmatchedBankStatementLines = await WorkspaceScope.ApplyTo(db.BankStatementLines.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && !item.Reconciled, cancellationToken);
        AddCheck(checks, issues, "Bank settlement acceptance", unreconciledBankTransactions == 0 && unmatchedBankStatementLines == 0, "Critical", $"Unreconciled bank transactions {unreconciledBankTransactions}, unmatched statement lines {unmatchedBankStatementLines}.", "/bank-reconciliation-closure");

        var openCreditNotes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.NoteType == NoteType.CreditNote && !item.IsAdjusted && item.OnDate < toExclusive, cancellationToken);
        var openDebitNotes = await WorkspaceScope.ApplyTo(db.CommercialNotes.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.NoteType == NoteType.DebitNote && !item.IsAdjusted && item.OnDate < toExclusive, cancellationToken);
        AddCheck(checks, issues, "Return and note acceptance", openCreditNotes == 0 && openDebitNotes == 0, "Warning", $"Open credit notes {openCreditNotes}, open debit notes {openDebitNotes}.", "/goods-return-acceptance");

        var monthlyAttendance = await WorkspaceScope.ApplyTo(db.AttendanceMonthlySummaries.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.Year == toDate.Year && item.Month == toDate.Month, cancellationToken);
        var payrollMonthStart = new DateTime(toDate.Year, toDate.Month, 1);
        var payrollMonthEnd = payrollMonthStart.AddMonths(1);
        var salaryPaySlips = await WorkspaceScope.ApplyTo(db.SalaryPaySlips.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.PayPeriodStart >= payrollMonthStart && item.PayPeriodStart < payrollMonthEnd, cancellationToken);
        AddCheck(checks, issues, "Attendance payroll acceptance", monthlyAttendance > 0 || salaryPaySlips > 0, "Warning", $"Monthly attendance summaries {monthlyAttendance}, salary payslips {salaryPaySlips} for {toDate:yyyy-MM}.", "/payroll");

        var fyLocks = await WorkspaceScope.ApplyTo(db.FinancialYearLocks.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.Active && item.PeriodStart <= fromDate && item.PeriodEnd >= toDate)
            .CountAsync(cancellationToken);
        AddCheck(checks, issues, "FY lock readiness", fyLocks > 0, "Warning", $"Matching active FY lock rows covering selected period: {fyLocks}.", "/financial-year-locks");

        var backupRows = new[]
        {
            "Database dump backup completed on production host.",
            "App-data volume/archive backup completed.",
            "Purchase-import proof archive verified.",
            "Restore drill completed on a test system."
        };
        AddCheck(checks, issues, "Backup restore drill", false, "Warning", "Manual proof required: backup and restore drill cannot be confirmed automatically from database rows.", "/backup-maintenance", false);

        var critical = issues.Count(item => item.Severity == "Critical");
        var warnings = issues.Count(item => item.Severity == "Warning");
        var status = critical == 0 ? "Ready for Owner Sign-off" : "Not Complete";
        var metrics = new List<ProductionGoLiveMetricDto>
        {
            new("Companies", companyCount, null, "Company masters in current workspace."),
            new("Stores", storeCount, null, "Store masters in current workspace."),
            new("Sales invoices", sales.Count, sales.Sum(item => item.BillAmount), "Accepted sales invoice evidence."),
            new("Purchase invoices", purchases.Count, purchases.Sum(item => item.BillAmount), "Accepted purchase inward evidence."),
            new("Stock rows", stocks.Count, null, "Stock master rows included in valuation check."),
            new("Critical issues", critical, null, "Blocks go-live."),
            new("Warnings", warnings, null, "Requires owner/accountant review.")
        };

        return new ProductionGoLiveMasterAcceptanceDto(
            status,
            AppInfoEndpoints.Version,
            AppInfoEndpoints.Stage,
            AppInfoEndpoints.BuildCode,
            generatedAtUtc,
            fromDate,
            toDate,
            metrics,
            checks,
            issues.Take(EvidenceLimit).ToList(),
            critical,
            warnings,
            backupRows,
            new List<string>
            {
                "Run Docker build/publish on the live host and confirm /api/health plus /api/app-info.",
                "Run all module closure pages and clear Critical issues first.",
                "Export this Go-Live CSV and Owner Closeout CSV for the owner file.",
                "Complete backup/restore drill on a test restore before final sign-off.",
                "Only then open Final Owner Sign-off and print/save the signed copy."
            },
            new List<string>
            {
                "This page reads operational database evidence; it does not create postings, locks or backups.",
                "Manual items such as restore drill, printer evidence and owner signature must still be verified by operator.",
                "Warning rows may be accepted by owner, but Critical rows should be cleared before go-live."
            },
            new List<string> { "Final Owner Sign-off", "Post-Go-Live Acceptance", "Production Support" });
    }

    private static void AddCheck(List<ProductionGoLiveCheckDto> checks, List<ProductionGoLiveIssueDto> issues, string area, bool passed, string severity, string message, string actionPath, bool addIssue = true)
    {
        checks.Add(new(area, passed ? "Complete" : (severity == "Critical" ? "Not Complete" : "Review"), severity, message, actionPath));
        if (!passed && addIssue) issues.Add(new(severity, area, message, actionPath));
    }

    private static string BuildCsv(ProductionGoLiveMasterAcceptanceDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Section,Status,Version,Stage,BuildCode,From,To,Critical,Warning,GeneratedAtUtc");
        csv.AppendLine($"Summary,{Csv(report.Status)},{Csv(report.Version)},{Csv(report.Stage)},{Csv(report.BuildCode)},{report.From:yyyy-MM-dd},{report.To:yyyy-MM-dd},{report.CriticalIssues},{report.WarningIssues},{report.GeneratedAtUtc:O}");
        csv.AppendLine();
        csv.AppendLine("Metric,Count,Amount,Description");
        foreach (var metric in report.Metrics) csv.AppendLine($"{Csv(metric.Label)},{metric.Count},{metric.Amount},{Csv(metric.Description)}");
        csv.AppendLine();
        csv.AppendLine("Area,Status,Severity,Message,ActionPath");
        foreach (var check in report.Checks) csv.AppendLine($"{Csv(check.Area)},{Csv(check.Status)},{Csv(check.Severity)},{Csv(check.Message)},{Csv(check.ActionPath)}");
        csv.AppendLine();
        csv.AppendLine("Severity,Title,Message,ActionPath");
        foreach (var issue in report.Issues) csv.AppendLine($"{Csv(issue.Severity)},{Csv(issue.Title)},{Csv(issue.Message)},{Csv(issue.ActionPath)}");
        csv.AppendLine();
        csv.AppendLine("ManualBackupRestoreProof");
        foreach (var item in report.ManualBackupRestoreProof) csv.AppendLine(Csv(item));
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

    private sealed record JournalLineProjection(decimal Debit, decimal Credit);
}

public sealed record ProductionGoLiveMasterAcceptanceDto(string Status, string Version, string Stage, string BuildCode, DateTimeOffset GeneratedAtUtc, DateTime From, DateTime To, List<ProductionGoLiveMetricDto> Metrics, List<ProductionGoLiveCheckDto> Checks, List<ProductionGoLiveIssueDto> Issues, int CriticalIssues, int WarningIssues, IReadOnlyList<string> ManualBackupRestoreProof, IReadOnlyList<string> CloseoutChecklist, IReadOnlyList<string> KnownLimitations, IReadOnlyList<string> NextModuleCandidates);
public sealed record ProductionGoLiveMetricDto(string Label, int? Count, decimal? Amount, string Description);
public sealed record ProductionGoLiveCheckDto(string Area, string Status, string Severity, string Message, string ActionPath);
public sealed record ProductionGoLiveIssueDto(string Severity, string Title, string Message, string ActionPath);

public static class FinalOwnerSignoffEndpoints
{
    public static RouteGroupBuilder MapFinalOwnerSignoffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/final-owner-signoff")
            .WithTags("Final Owner Sign-off")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("", GetAsync);
        group.MapGet("/evidence.csv", ExportCsvAsync);
        group.MapGet("/print", PrintAsync);
        return group;
    }

    private static async Task<FinalOwnerSignoffDto> GetAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? ownerName = null,
        string? ownerMobile = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildAsync(context, db, companyId, storeGroupId, storeId, from, to, ownerName, ownerMobile, cancellationToken);
    }

    private static async Task<IResult> ExportCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? ownerName = null,
        string? ownerMobile = null,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildAsync(context, db, companyId, storeGroupId, storeId, from, to, ownerName, ownerMobile, cancellationToken);
        var fileName = $"garmetix-final-owner-signoff-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(BuildCsv(report)), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<IResult> PrintAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? ownerName = null,
        string? ownerMobile = null,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildAsync(context, db, companyId, storeGroupId, storeId, from, to, ownerName, ownerMobile, cancellationToken);
        var html = BuildPrintHtml(report);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static async Task<FinalOwnerSignoffDto> BuildAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? ownerName,
        string? ownerMobile,
        CancellationToken cancellationToken)
    {
        var goLive = await ProductionGoLiveMasterAcceptanceEndpointsForSignoff.BuildSignoffSnapshotAsync(context, db, companyId, storeGroupId, storeId, from, to, cancellationToken);
        var ready = goLive.CriticalIssues == 0;
        var status = ready ? "Ready to Sign" : "Not Ready";
        var signoffRows = new List<FinalOwnerSignoffRowDto>
        {
            new("Build/version", AppInfoEndpoints.Version, AppInfoEndpoints.BuildCode, "Confirm deployed version and build code match this package."),
            new("Go-live acceptance", goLive.Status, $"Critical {goLive.CriticalIssues}, Warning {goLive.WarningIssues}", "Critical issues must be zero before final signature."),
            new("Owner closeout", ready ? "Accepted with evidence" : "Blocked", "Attach Owner Closeout CSV", "Owner/accountant keeps exported closeout evidence."),
            new("Backup restore", "Manual sign-off required", "Attach restore proof", "Do not sign without successful test restore proof."),
            new("Printer/PDF", "Manual sign-off required", "Attach accepted print evidence", "Confirm sale/purchase print/PDF samples."),
            new("Post-go-live support", "Prepared", "Use Production Support and Post-Go-Live Acceptance", "Monitor first live days and record issues."),
        };

        return new FinalOwnerSignoffDto(
            status,
            ready,
            AppInfoEndpoints.Version,
            AppInfoEndpoints.Stage,
            AppInfoEndpoints.BuildCode,
            DateTimeOffset.UtcNow,
            goLive.From,
            goLive.To,
            ownerName?.Trim() ?? string.Empty,
            ownerMobile?.Trim() ?? string.Empty,
            goLive.CriticalIssues,
            goLive.WarningIssues,
            signoffRows,
            goLive.Issues.Take(50).ToList(),
            new List<string>
            {
                "I confirm the selected period has been reviewed by owner/accountant.",
                "I confirm Critical issues are cleared or go-live has been explicitly deferred.",
                "I confirm backup, restore, printer/PDF and access-control evidence is kept outside the app.",
                "I understand this sign-off page does not lock the database; FY Locks and module locks remain separate controls."
            },
            new List<string> { "Owner signature", "Date/time", "Mobile/identity", "Witness/accountant signature", "Backup restore proof reference" },
            new List<string> { "/production-go-live-master-acceptance", "/owner-closeout-command-center", "/post-go-live-acceptance", "/production-support" });
    }

    private static string BuildCsv(FinalOwnerSignoffDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Section,Status,ReadyToSign,Version,Stage,BuildCode,From,To,OwnerName,OwnerMobile,Critical,Warning,GeneratedAtUtc");
        csv.AppendLine($"Summary,{Csv(report.Status)},{report.ReadyToSign},{Csv(report.Version)},{Csv(report.Stage)},{Csv(report.BuildCode)},{report.From:yyyy-MM-dd},{report.To:yyyy-MM-dd},{Csv(report.OwnerName)},{Csv(report.OwnerMobile)},{report.CriticalIssues},{report.WarningIssues},{report.GeneratedAtUtc:O}");
        csv.AppendLine();
        csv.AppendLine("Area,Status,Evidence,OwnerInstruction");
        foreach (var row in report.SignoffRows) csv.AppendLine($"{Csv(row.Area)},{Csv(row.Status)},{Csv(row.Evidence)},{Csv(row.OwnerInstruction)}");
        csv.AppendLine();
        csv.AppendLine("Declarations");
        foreach (var item in report.OwnerDeclarations) csv.AppendLine(Csv(item));
        csv.AppendLine();
        csv.AppendLine("SignatureFields");
        foreach (var item in report.SignatureFields) csv.AppendLine(Csv(item));
        csv.AppendLine();
        csv.AppendLine("OpenIssueSeverity,Title,Message,ActionPath");
        foreach (var issue in report.OpenIssues) csv.AppendLine($"{Csv(issue.Severity)},{Csv(issue.Title)},{Csv(issue.Message)},{Csv(issue.ActionPath)}");
        return csv.ToString();
    }

    private static string BuildPrintHtml(FinalOwnerSignoffDto report)
    {
        static string H(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
        var rows = string.Join("", report.SignoffRows.Select(row => $"<tr><td>{H(row.Area)}</td><td>{H(row.Status)}</td><td>{H(row.Evidence)}</td><td>{H(row.OwnerInstruction)}</td></tr>"));
        var declarations = string.Join("", report.OwnerDeclarations.Select(item => $"<li>{H(item)}</li>"));
        var issues = report.OpenIssues.Count == 0
            ? "<p>No open Critical/Warning issue rows in sign-off snapshot.</p>"
            : $"<ul>{string.Join("", report.OpenIssues.Select(item => $"<li><strong>{H(item.Severity)}</strong> - {H(item.Title)}: {H(item.Message)}</li>"))}</ul>";
        var ownerLine = !string.IsNullOrWhiteSpace(report.OwnerMobile)
            ? $"{H(report.OwnerName)} / {H(report.OwnerMobile)}"
            : H(report.OwnerName);

        var html = new StringBuilder();
        html.AppendLine("<!doctype html>");
        html.AppendLine("""<html><head><meta charset="utf-8"><title>Garmetix Final Owner Sign-off</title>""");
        html.AppendLine("""<style>body{font-family:Arial,sans-serif;margin:24px;color:#111}h1{margin:0 0 6px}.muted{color:#555}.badge{display:inline-block;padding:6px 10px;border:1px solid #111;border-radius:999px;font-weight:700}table{width:100%;border-collapse:collapse;margin-top:16px}th,td{border:1px solid #bbb;padding:8px;text-align:left;vertical-align:top}.sign{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin-top:34px}.box{border-top:1px solid #111;padding-top:8px;min-height:48px}@media print{button{display:none}}</style>""");
        html.AppendLine("</head><body>");
        html.AppendLine("""<button onclick="window.print()">Print / Save PDF</button>""");
        html.AppendLine("<h1>Garmetix Final Owner Sign-off</h1>");
        html.AppendLine($"""<p class="muted">Version {H(report.Version)} / {H(report.BuildCode)} / {H(report.Stage)}</p>""");
        html.AppendLine($"""<p><span class="badge">{H(report.Status)}</span></p>""");
        html.AppendLine($"<p>Period: {report.From:dd-MMM-yyyy} to {report.To:dd-MMM-yyyy}. Generated UTC: {report.GeneratedAtUtc:O}</p>");
        html.AppendLine($"<p>Owner: {ownerLine}</p>");
        html.AppendLine($"<table><thead><tr><th>Area</th><th>Status</th><th>Evidence</th><th>Owner instruction</th></tr></thead><tbody>{rows}</tbody></table>");
        html.AppendLine($"<h2>Owner declarations</h2><ul>{declarations}</ul>");
        html.AppendLine($"<h2>Open issue snapshot</h2>{issues}");
        html.AppendLine("""<div class="sign"><div class="box">Owner signature / date</div><div class="box">Accountant / witness signature</div><div class="box">Backup restore proof reference</div><div class="box">Print/PDF proof reference</div></div>""");
        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private static string Csv(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}

internal static class ProductionGoLiveMasterAcceptanceEndpointsForSignoff
{
    internal static async Task<ProductionGoLiveSignoffSnapshotDto> BuildSignoffSnapshotAsync(HttpContext context, GarmetixDbContext db, Guid? companyId, Guid? storeGroupId, Guid? storeId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var (fromDate, toDate, toExclusive) = ResolveDateRange(from, to);
        var issues = new List<ProductionGoLiveIssueDto>();

        var salesQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context).Where(item => !item.Deleted && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return && item.InvoiceStatus != InvoiceStatus.Cancelled && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) salesQuery = salesQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) salesQuery = salesQuery.Where(item => item.StoreId == storeId.Value);
        if (storeGroupId.HasValue) salesQuery = salesQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        var salesCount = await salesQuery.CountAsync(cancellationToken);
        if (salesCount == 0) issues.Add(new("Warning", "No sale invoice evidence", "No active sales were found in the selected go-live period.", "/billing/final-qa"));

        var negativeStocks = await WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context).CountAsync(item => !item.Deleted && (!storeId.HasValue || item.StoreId == storeId.Value) && item.PurchaseQty - item.SoldQty < 0, cancellationToken);
        if (negativeStocks > 0) issues.Add(new("Critical", "Negative stock", $"{negativeStocks} stock row(s) are negative.", "/inventory/stock-valuation-closure"));

        var unreconciledBankTransactions = await WorkspaceScope.ApplyTo(db.BankTransactions.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && !item.Reconciled, cancellationToken);
        var unmatchedBankStatementLines = await WorkspaceScope.ApplyTo(db.BankStatementLines.AsNoTracking(), context).CountAsync(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive && !item.Reconciled, cancellationToken);
        if (unreconciledBankTransactions + unmatchedBankStatementLines > 0) issues.Add(new("Critical", "Bank reconciliation pending", $"{unreconciledBankTransactions} bank transaction(s) and {unmatchedBankStatementLines} statement line(s) are unreconciled.", "/bank-reconciliation-closure"));

        var journalIds = await WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context).Where(item => !item.Deleted && item.OnDate >= fromDate && item.OnDate < toExclusive).Select(item => item.Id).ToListAsync(cancellationToken);
        var journalLines = journalIds.Count == 0 ? new List<JournalLineProjection>() : await WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context).Where(item => !item.Deleted && journalIds.Contains(item.JournalEntryId)).Select(item => new JournalLineProjection(item.Debit, item.Credit)).ToListAsync(cancellationToken);
        var diff = Math.Abs(journalLines.Sum(item => item.Debit) - journalLines.Sum(item => item.Credit));
        if (diff > 1) issues.Add(new("Critical", "Journal imbalance", $"Journal debit/credit difference is {diff:0.00}.", "/accounting-gst-validation"));

        var critical = issues.Count(item => item.Severity == "Critical");
        var warning = issues.Count(item => item.Severity == "Warning");
        return new ProductionGoLiveSignoffSnapshotDto(critical == 0 ? "Ready for Owner Sign-off" : "Not Complete", fromDate, toDate, critical, warning, issues);
    }

    private static (DateTime FromDate, DateTime ToDate, DateTime ToExclusive) ResolveDateRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toDate = (to ?? today).Date;
        if (toDate < fromDate) (fromDate, toDate) = (toDate, fromDate);
        return (fromDate, toDate, toDate.AddDays(1));
    }

    private sealed record JournalLineProjection(decimal Debit, decimal Credit);
}

public sealed record ProductionGoLiveSignoffSnapshotDto(string Status, DateTime From, DateTime To, int CriticalIssues, int WarningIssues, List<ProductionGoLiveIssueDto> Issues);
public sealed record FinalOwnerSignoffDto(string Status, bool ReadyToSign, string Version, string Stage, string BuildCode, DateTimeOffset GeneratedAtUtc, DateTime From, DateTime To, string OwnerName, string OwnerMobile, int CriticalIssues, int WarningIssues, List<FinalOwnerSignoffRowDto> SignoffRows, List<ProductionGoLiveIssueDto> OpenIssues, IReadOnlyList<string> OwnerDeclarations, IReadOnlyList<string> SignatureFields, IReadOnlyList<string> EvidenceLinks);
public sealed record FinalOwnerSignoffRowDto(string Area, string Status, string Evidence, string OwnerInstruction);
