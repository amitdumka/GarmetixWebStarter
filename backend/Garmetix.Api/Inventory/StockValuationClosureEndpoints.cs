using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Inventory;

public static class StockValuationClosureEndpoints
{
    private const int EvidenceLimit = 500;

    public static RouteGroupBuilder MapStockValuationClosureEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/inventory/stock-valuation-closure")
            .WithTags("Stock Valuation Closure")
            .RequireAuthorization(GarmetixPolicies.Inventory);

        group.MapGet("", GetClosureAsync);
        group.MapGet("/evidence.csv", ExportCsvAsync);
        return group;
    }

    private static async Task<StockValuationClosureDto> GetClosureAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildReportAsync(context, db, companyId, storeGroupId, storeId, asOf, cancellationToken);
    }

    private static async Task<IResult> ExportCsvAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId = null,
        Guid? storeGroupId = null,
        Guid? storeId = null,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildReportAsync(context, db, companyId, storeGroupId, storeId, asOf, cancellationToken);
        var csv = BuildCsv(report);
        var fileName = $"garmetix-stock-valuation-closure-{report.AsOf:yyyyMMdd}.csv";
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
    }

    private static async Task<StockValuationClosureDto> BuildReportAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? asOf,
        CancellationToken cancellationToken)
    {
        var asOfDate = (asOf ?? DateTime.Today).Date;
        var toExclusive = asOfDate.AddDays(1);
        var issues = new List<StockValuationIssueDto>();

        var stocksQuery = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        if (companyId.HasValue) stocksQuery = stocksQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) stocksQuery = stocksQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) stocksQuery = stocksQuery.Where(item => item.StoreId == storeId.Value);
        var stocks = await stocksQuery.OrderBy(item => item.Barcode).ToListAsync(cancellationToken);

        var productIds = stocks.Select(item => item.ProductId).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<Guid, Garmetix.Core.Models.Inventory.Product>()
            : await WorkspaceScope.ApplyTo(db.Products.AsNoTracking(), context)
                .Where(item => !item.Deleted && productIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item, cancellationToken);

        var stockIds = stocks.Select(item => item.Id).Distinct().ToList();
        var movementQuery = WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.OnDate < toExclusive);
        if (companyId.HasValue) movementQuery = movementQuery.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) movementQuery = movementQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) movementQuery = movementQuery.Where(item => item.StoreId == storeId.Value);
        var movements = await movementQuery.ToListAsync(cancellationToken);
        var movementsByStock = movements.Where(item => item.StockId.HasValue).GroupBy(item => item.StockId!.Value).ToDictionary(group => group.Key, group => group.ToList());
        var movementsByProductBarcode = movements.GroupBy(item => (item.ProductId, Barcode: NormalizeBarcode(item.Barcode))).ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<StockValuationClosureRowDto>();
        foreach (var stock in stocks)
        {
            products.TryGetValue(stock.ProductId, out var product);
            var currentQty = stock.PurchaseQty - stock.SoldQty;
            var costValue = Math.Round(currentQty * stock.CostPrice, 2);
            var mrpValue = Math.Round(currentQty * stock.MRP, 2);
            var movementRows = movementsByStock.TryGetValue(stock.Id, out var byStock)
                ? byStock
                : movementsByProductBarcode.TryGetValue((stock.ProductId, NormalizeBarcode(stock.Barcode)), out var byProductBarcode) ? byProductBarcode : new List<Garmetix.Core.Models.Inventory.StockMovement>();
            var movementQty = Math.Round(movementRows.Sum(item => item.QuantityIn - item.QuantityOut), 3);
            var movementValue = Math.Round(movementRows.OrderByDescending(item => item.OnDate).FirstOrDefault()?.InventoryValueAfter ?? costValue, 2);
            var status = currentQty < 0 ? "Negative stock" : stock.CostPrice <= 0 && currentQty > 0 ? "Missing cost" : movementRows.Count == 0 && currentQty != 0 ? "No movement proof" : stock.MRP > 0 && stock.CostPrice > stock.MRP ? "Cost above MRP" : "OK";

            if (currentQty < 0)
            {
                issues.Add(new StockValuationIssueDto("Critical", "Negative stock", $"{stock.Barcode} has current stock {currentQty}.", "/stock-reports"));
            }
            if (stock.CostPrice <= 0 && currentQty > 0)
            {
                issues.Add(new StockValuationIssueDto("Critical", "Missing cost price", $"{stock.Barcode} has stock but zero cost price; valuation cannot be trusted.", "/inventory"));
            }
            if (movementRows.Count == 0 && currentQty != 0)
            {
                issues.Add(new StockValuationIssueDto("Warning", "No movement proof", $"{stock.Barcode} has current stock but no stock movement history up to {asOfDate:yyyy-MM-dd}.", "/stock-reports"));
            }
            if (stock.MRP > 0 && stock.CostPrice > stock.MRP)
            {
                issues.Add(new StockValuationIssueDto("Warning", "Cost above MRP", $"{stock.Barcode} cost price is above MRP.", "/inventory"));
            }
            if (product is not null && Math.Abs(product.TaxRate - stock.TaxRate) > 0.001m)
            {
                issues.Add(new StockValuationIssueDto("Warning", "Tax rate mismatch", $"{stock.Barcode} stock tax {stock.TaxRate}% differs from product tax {product.TaxRate}%.", "/inventory"));
            }

            rows.Add(new StockValuationClosureRowDto(
                stock.Id,
                stock.ProductId,
                product?.Name ?? stock.Barcode,
                stock.Barcode,
                stock.HSNCode ?? product?.HSNCode ?? string.Empty,
                stock.StockType.ToString(),
                stock.PurchaseQty,
                stock.SoldQty,
                currentQty,
                stock.CostPrice,
                stock.MRP,
                stock.TaxRate,
                costValue,
                mrpValue,
                movementRows.Count,
                movementQty,
                movementValue,
                status));
        }

        var summary = rows.GroupBy(item => item.StockType)
            .Select(group => new StockValuationClosureSummaryDto(group.Key, group.Count(), group.Sum(item => item.CurrentQty), group.Sum(item => item.CostValue), group.Sum(item => item.MrpValue)))
            .OrderBy(item => item.StockType)
            .ToList();

        var statusOverall = issues.Any(item => item.Severity == "Critical") ? "Not Complete" : "Complete";
        var metrics = new List<StockValuationMetricDto>
        {
            new("Stock rows", rows.Count, null, "Rows in stock master."),
            new("Quantity on hand", null, rows.Sum(item => item.CurrentQty), "Current stock quantity."),
            new("Cost value", null, rows.Sum(item => item.CostValue), "Current stock valued at cost."),
            new("MRP value", null, rows.Sum(item => item.MrpValue), "Current stock valued at MRP."),
            new("Negative rows", issues.Count(item => item.Title == "Negative stock"), null, "Critical stock rows."),
            new("Missing cost rows", issues.Count(item => item.Title == "Missing cost price"), null, "Critical valuation rows.")
        };

        return new StockValuationClosureDto(
            statusOverall,
            asOfDate,
            metrics,
            issues.Count(item => item.Severity == "Critical"),
            issues.Count(item => item.Severity == "Warning"),
            issues.OrderBy(item => item.Severity == "Critical" ? 0 : 1).Take(EvidenceLimit).ToList(),
            summary,
            rows.OrderBy(item => item.Status == "OK" ? 1 : 0).ThenBy(item => item.ProductName).Take(EvidenceLimit).ToList(),
            new List<string>
            {
                "Clear negative stock rows.",
                "Correct zero-cost stock before FY/month close.",
                "Confirm movement history exists for live stock.",
                "Export CSV and attach with stock report/FY closeout evidence.",
                "Run Profit/Loss report again after cost corrections."
            },
            new List<string>
            {
                "This stage validates current stock master and movement evidence; it does not recalculate or mutate stock.",
                "Movement quantity and current stock can differ for older imported/opening data; mismatch is visible as review evidence.",
                "Physical stock counting and barcode scanning are operational controls outside this read-only closure."
            },
            new List<string> { "Owner Closeout Command Center", "Profit/Loss Reporting", "Financial Year Closeout" });
    }

    private static string BuildCsv(StockValuationClosureDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Section,Status,AsOf,Critical,Warning");
        csv.AppendLine($"Summary,{Csv(report.Status)},{report.AsOf:yyyy-MM-dd},{report.CriticalIssues},{report.WarningIssues}");
        csv.AppendLine();
        csv.AppendLine("Metric,Count,Amount,Description");
        foreach (var metric in report.Metrics) csv.AppendLine($"{Csv(metric.Label)},{metric.Count},{metric.Amount},{Csv(metric.Description)}");
        csv.AppendLine();
        csv.AppendLine("Severity,Title,Message,ActionPath");
        foreach (var issue in report.Issues) csv.AppendLine($"{Csv(issue.Severity)},{Csv(issue.Title)},{Csv(issue.Message)},{Csv(issue.ActionPath)}");
        csv.AppendLine();
        csv.AppendLine("StockType,Rows,Quantity,CostValue,MrpValue");
        foreach (var row in report.Summary) csv.AppendLine($"{Csv(row.StockType)},{row.Rows},{row.Quantity},{row.CostValue},{row.MrpValue}");
        csv.AppendLine();
        csv.AppendLine("ProductName,Barcode,HSN,StockType,PurchaseQty,SoldQty,CurrentQty,CostPrice,MRP,TaxRate,CostValue,MrpValue,MovementRows,MovementQty,MovementValue,Status");
        foreach (var row in report.Rows)
        {
            csv.AppendLine($"{Csv(row.ProductName)},{Csv(row.Barcode)},{Csv(row.HsnCode)},{Csv(row.StockType)},{row.PurchaseQty},{row.SoldQty},{row.CurrentQty},{row.CostPrice},{row.Mrp},{row.TaxRate},{row.CostValue},{row.MrpValue},{row.MovementRows},{row.MovementQty},{row.MovementValue},{Csv(row.Status)}");
        }
        return csv.ToString();
    }

    private static string NormalizeBarcode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string Csv(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r') ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}

public sealed record StockValuationClosureDto(string Status, DateTime AsOf, List<StockValuationMetricDto> Metrics, int CriticalIssues, int WarningIssues, List<StockValuationIssueDto> Issues, List<StockValuationClosureSummaryDto> Summary, List<StockValuationClosureRowDto> Rows, List<string> CloseoutChecklist, List<string> KnownLimitations, List<string> NextModuleCandidates);
public sealed record StockValuationMetricDto(string Label, int? Count, decimal? Amount, string Description);
public sealed record StockValuationIssueDto(string Severity, string Title, string Message, string ActionPath);
public sealed record StockValuationClosureSummaryDto(string StockType, int Rows, decimal Quantity, decimal CostValue, decimal MrpValue);
public sealed record StockValuationClosureRowDto(Guid StockId, Guid ProductId, string ProductName, string Barcode, string HsnCode, string StockType, decimal PurchaseQty, decimal SoldQty, decimal CurrentQty, decimal CostPrice, decimal Mrp, decimal TaxRate, decimal CostValue, decimal MrpValue, int MovementRows, decimal MovementQty, decimal MovementValue, string Status);
