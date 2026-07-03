using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Core.Models.Inventory;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Reports;

public static class ProfitLossReportEndpoints
{
    private const decimal AmountTolerance = 1.00m;
    private const int EvidenceLimit = 500;

    public static RouteGroupBuilder MapProfitLossReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reports/profit-loss")
            .WithTags("Profit Loss Reports")
            .RequireAuthorization(GarmetixPolicies.Accounting);

        group.MapGet("", GetReportAsync);
        group.MapGet("/evidence.csv", ExportCsvAsync);
        return group;
    }

    private static async Task<ProfitLossReportDto> GetReportAsync(
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
        var fileName = $"garmetix-profit-loss-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<ProfitLossReportDto> BuildReportAsync(
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
        var issues = new List<ProfitLossIssueDto>();

        var salesQuery = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.InvoiceType != InvoiceType.Return && item.InvoiceStatus != InvoiceStatus.Cancelled && item.OnDate >= fromDate && item.OnDate < toExclusive);
        if (companyId.HasValue) salesQuery = salesQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeId.HasValue) salesQuery = salesQuery.Where(item => item.StoreId == storeId.Value);
        if (storeGroupId.HasValue) salesQuery = salesQuery.Where(item => db.Stores.Any(store => store.Id == item.StoreId && store.StoreGroupId == storeGroupId.Value));
        var sales = await salesQuery.OrderBy(item => item.OnDate).ThenBy(item => item.InvoiceNumber).ToListAsync(cancellationToken);
        var saleIds = sales.Select(item => item.Id).Distinct().ToList();
        var saleById = sales.ToDictionary(item => item.Id, item => item);

        var items = saleIds.Count == 0
            ? new List<InvoiceItem>()
            : await WorkspaceScope.ApplyTo(db.InvoiceItems.AsNoTracking(), context)
                .Where(item => !item.Deleted && saleIds.Contains(item.InvoiceId))
                .OrderBy(item => item.InvoiceId)
                .ThenBy(item => item.Barcode)
                .ToListAsync(cancellationToken);

        var stockMoves = saleIds.Count == 0
            ? new List<StockMovement>()
            : await WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
                .Where(item => !item.Deleted && item.SourceId.HasValue && saleIds.Contains(item.SourceId.Value) && item.QuantityOut > 0)
                .ToListAsync(cancellationToken);

        var productIds = items.Select(item => item.ProductId).Concat(stockMoves.Select(item => item.ProductId)).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<Guid, Product>()
            : await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
                .Where(item => !item.Deleted && productIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item, cancellationToken);

        var movesByInvoiceProduct = stockMoves
            .Where(item => item.SourceId.HasValue)
            .GroupBy(item => (InvoiceId: item.SourceId!.Value, item.ProductId, Barcode: NormalizeBarcode(item.Barcode)))
            .ToDictionary(group => group.Key, group => group.ToList());

        var itemRows = new List<ProfitLossItemRowDto>();
        foreach (var item in items)
        {
            saleById.TryGetValue(item.InvoiceId, out var invoice);
            products.TryGetValue(item.ProductId, out var product);
            var key = (InvoiceId: item.InvoiceId, item.ProductId, Barcode: NormalizeBarcode(item.Barcode));
            movesByInvoiceProduct.TryGetValue(key, out var moves);
            moves ??= stockMoves.Where(move => move.SourceId == item.InvoiceId && move.ProductId == item.ProductId).ToList();

            var billedQty = item.BilledQuantity == 0 ? 1m : item.BilledQuantity;
            var stockOutQty = moves.Sum(move => move.QuantityOut);
            var costAmount = moves.Sum(move => move.CostPrice * move.QuantityOut);
            if (costAmount == 0 && moves.Count > 0)
            {
                costAmount = moves.Sum(move => move.CostImpact < 0 ? Math.Abs(move.CostImpact) : move.CostPrice * move.QuantityOut);
            }

            var revenueExTax = Math.Round(item.Amount - item.TaxAmount, 2);
            var revenueIncTax = Math.Round(item.Amount, 2);
            var grossProfit = Math.Round(revenueExTax - costAmount, 2);
            var marginPercent = revenueExTax == 0 ? 0 : Math.Round(grossProfit / revenueExTax * 100, 2);
            var evidenceStatus = moves.Count == 0 ? "Missing stock-out cost" : Math.Abs(stockOutQty - billedQty) > 0.001m ? "Quantity mismatch" : grossProfit < 0 ? "Loss" : "OK";

            if (moves.Count == 0)
            {
                issues.Add(new ProfitLossIssueDto("Critical", "Missing stock-out cost", $"Invoice {invoice?.InvoiceNumber ?? item.InvoiceId.ToString("N")} item {item.Barcode} has no linked stock-out cost evidence.", "/reports/profit-loss"));
            }
            else if (Math.Abs(stockOutQty - billedQty) > 0.001m)
            {
                issues.Add(new ProfitLossIssueDto("Warning", "Stock-out quantity mismatch", $"Invoice {invoice?.InvoiceNumber ?? item.InvoiceId.ToString("N")} item {item.Barcode}: billed {billedQty}, stock-out {stockOutQty}.", "/stock-reports"));
            }

            if (grossProfit < 0)
            {
                issues.Add(new ProfitLossIssueDto("Warning", "Negative item margin", $"Invoice {invoice?.InvoiceNumber ?? item.InvoiceId.ToString("N")} item {item.Barcode} shows loss of {grossProfit:0.00}.", "/reports/profit-loss"));
            }

            if (string.IsNullOrWhiteSpace(item.Barcode))
            {
                issues.Add(new ProfitLossIssueDto("Warning", "Missing barcode", $"Invoice {invoice?.InvoiceNumber ?? item.InvoiceId.ToString("N")} has an item without barcode evidence.", "/billing/final-qa"));
            }

            itemRows.Add(new ProfitLossItemRowDto(
                item.InvoiceId,
                invoice?.InvoiceNumber ?? item.InvoiceId.ToString("N"),
                invoice?.OnDate ?? DateTime.MinValue,
                invoice?.CustomerName ?? invoice?.CustomerMobileNumber ?? "Customer",
                item.ProductId,
                product?.Name ?? item.ProductName ?? item.Barcode,
                item.Barcode,
                billedQty,
                revenueIncTax,
                item.TaxAmount,
                revenueExTax,
                costAmount,
                grossProfit,
                marginPercent,
                stockOutQty,
                evidenceStatus));
        }

        foreach (var sale in sales.Where(sale => !items.Any(item => item.InvoiceId == sale.Id)))
        {
            issues.Add(new ProfitLossIssueDto("Critical", "Sale invoice has no item rows", $"Invoice {sale.InvoiceNumber} has no item rows, so profit cannot be verified.", "/billing/final-qa"));
        }

        var invoiceRows = itemRows
            .GroupBy(item => new { item.InvoiceId, item.InvoiceNumber, item.InvoiceDate, item.CustomerName })
            .Select(group => new ProfitLossInvoiceRowDto(
                group.Key.InvoiceId,
                group.Key.InvoiceNumber,
                group.Key.InvoiceDate,
                group.Key.CustomerName,
                group.Count(),
                group.Sum(item => item.Quantity),
                group.Sum(item => item.RevenueIncTax),
                group.Sum(item => item.TaxAmount),
                group.Sum(item => item.RevenueExTax),
                group.Sum(item => item.CostAmount),
                group.Sum(item => item.GrossProfit),
                group.Sum(item => item.RevenueExTax) == 0 ? 0 : Math.Round(group.Sum(item => item.GrossProfit) / group.Sum(item => item.RevenueExTax) * 100, 2),
                group.Any(item => item.EvidenceStatus == "Missing stock-out cost") ? "Needs cost proof" : group.Any(item => item.GrossProfit < 0) ? "Loss review" : "OK"))
            .OrderByDescending(item => item.InvoiceDate)
            .Take(EvidenceLimit)
            .ToList();

        var revenue = itemRows.Sum(item => item.RevenueExTax);
        var grossProfitTotal = itemRows.Sum(item => item.GrossProfit);
        var costTotal = itemRows.Sum(item => item.CostAmount);
        var status = issues.Any(item => item.Severity == "Critical") ? "Not Complete" : "Complete";
        var metrics = new List<ProfitLossMetricDto>
        {
            new("Sale invoices", sales.Count, null, "Non-cancelled sales in selected range."),
            new("Sale items", itemRows.Count, null, "Item-level profit evidence rows."),
            new("Revenue ex GST", null, revenue, "Tax-exclusive sales revenue."),
            new("COGS", null, costTotal, "Cost from linked stock-out movements."),
            new("Gross profit", null, grossProfitTotal, "Revenue ex GST minus COGS."),
            new("Gross margin %", null, revenue == 0 ? 0 : Math.Round(grossProfitTotal / revenue * 100, 2), "Gross profit as percentage of revenue ex GST.")
        };

        return new ProfitLossReportDto(
            status,
            fromDate,
            toDate,
            metrics,
            issues.Count(item => item.Severity == "Critical"),
            issues.Count(item => item.Severity == "Warning"),
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).Take(EvidenceLimit).ToList(),
            invoiceRows,
            itemRows.OrderByDescending(item => item.InvoiceDate).Take(EvidenceLimit).ToList(),
            new List<string>
            {
                "Confirm all sale invoices have item rows.",
                "Confirm every sale item has stock-out cost evidence.",
                "Review negative margin items before month/FY close.",
                "Export CSV and keep with Day Book/FY closeout evidence.",
                "Use Stock Valuation Closure if cost evidence is missing."
            },
            new List<string>
            {
                "Profit is based on stock movement cost evidence. If old imports did not create stock-out cost rows, missing-cost issues are shown.",
                "This report does not mutate invoices, stock, journal entries or GST records.",
                "Operational expenses are not allocated to item-wise profit in this stage; this is gross profit / COGS reporting."
            },
            new List<string> { "Stock Valuation Closure", "Owner Closeout Command Center", "Financial Year Closeout" });
    }

    private static string BuildCsv(ProfitLossReportDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Section,Status,From,To,Critical,Warning");
        csv.AppendLine($"Summary,{Csv(report.Status)},{report.From:yyyy-MM-dd},{report.To:yyyy-MM-dd},{report.CriticalIssues},{report.WarningIssues}");
        csv.AppendLine();
        csv.AppendLine("Metric,Count,Amount,Description");
        foreach (var metric in report.Metrics)
        {
            csv.AppendLine($"{Csv(metric.Label)},{metric.Count},{metric.Amount},{Csv(metric.Description)}");
        }
        csv.AppendLine();
        csv.AppendLine("Severity,Title,Message,ActionPath");
        foreach (var issue in report.Issues)
        {
            csv.AppendLine($"{Csv(issue.Severity)},{Csv(issue.Title)},{Csv(issue.Message)},{Csv(issue.ActionPath)}");
        }
        csv.AppendLine();
        csv.AppendLine("InvoiceDate,InvoiceNumber,Customer,ItemCount,Quantity,RevenueIncTax,TaxAmount,RevenueExTax,COGS,GrossProfit,MarginPercent,EvidenceStatus");
        foreach (var row in report.InvoiceRows)
        {
            csv.AppendLine($"{row.InvoiceDate:yyyy-MM-dd},{Csv(row.InvoiceNumber)},{Csv(row.CustomerName)},{row.ItemCount},{row.Quantity},{row.RevenueIncTax},{row.TaxAmount},{row.RevenueExTax},{row.CostAmount},{row.GrossProfit},{row.MarginPercent},{Csv(row.EvidenceStatus)}");
        }
        csv.AppendLine();
        csv.AppendLine("InvoiceDate,InvoiceNumber,Customer,ProductName,Barcode,Quantity,RevenueIncTax,TaxAmount,RevenueExTax,COGS,GrossProfit,MarginPercent,StockOutQuantity,EvidenceStatus");
        foreach (var row in report.ItemRows)
        {
            csv.AppendLine($"{row.InvoiceDate:yyyy-MM-dd},{Csv(row.InvoiceNumber)},{Csv(row.CustomerName)},{Csv(row.ProductName)},{Csv(row.Barcode)},{row.Quantity},{row.RevenueIncTax},{row.TaxAmount},{row.RevenueExTax},{row.CostAmount},{row.GrossProfit},{row.MarginPercent},{row.StockOutQuantity},{Csv(row.EvidenceStatus)}");
        }
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

    private static string NormalizeBarcode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string Csv(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}

public sealed record ProfitLossReportDto(
    string Status,
    DateTime From,
    DateTime To,
    List<ProfitLossMetricDto> Metrics,
    int CriticalIssues,
    int WarningIssues,
    List<ProfitLossIssueDto> Issues,
    List<ProfitLossInvoiceRowDto> InvoiceRows,
    List<ProfitLossItemRowDto> ItemRows,
    List<string> CloseoutChecklist,
    List<string> KnownLimitations,
    List<string> NextModuleCandidates);

public sealed record ProfitLossMetricDto(string Label, int? Count, decimal? Amount, string Description);
public sealed record ProfitLossIssueDto(string Severity, string Title, string Message, string ActionPath);
public sealed record ProfitLossInvoiceRowDto(Guid InvoiceId, string InvoiceNumber, DateTime InvoiceDate, string CustomerName, int ItemCount, decimal Quantity, decimal RevenueIncTax, decimal TaxAmount, decimal RevenueExTax, decimal CostAmount, decimal GrossProfit, decimal MarginPercent, string EvidenceStatus);
public sealed record ProfitLossItemRowDto(Guid InvoiceId, string InvoiceNumber, DateTime InvoiceDate, string CustomerName, Guid ProductId, string ProductName, string Barcode, decimal Quantity, decimal RevenueIncTax, decimal TaxAmount, decimal RevenueExTax, decimal CostAmount, decimal GrossProfit, decimal MarginPercent, decimal StockOutQuantity, string EvidenceStatus);
