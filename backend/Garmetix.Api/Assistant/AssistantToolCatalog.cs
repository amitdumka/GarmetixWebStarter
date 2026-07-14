using System.Text.Json;
using Garmetix.Api.Dashboard;
using Garmetix.Api.Workspace;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Assistant;

/// <summary>
/// Read-only tool set the Garmetix Assistant can call. Every tool runs the
/// same WorkspaceScope.ApplyTo(...) scoping used by the rest of the API, so
/// a store-scoped user's assistant session can only ever see that user's
/// permitted companies / store groups / stores - identical to what they
/// would see in the normal UI. There are no write/mutating tools yet; add
/// those behind an explicit confirmation step (see README-assistant.md).
/// </summary>
public sealed class AssistantToolCatalog(GarmetixDbContext db)
{
    public IReadOnlyList<object> ToolSchemas { get; } =
    [
        new
        {
            name = "get_store_sales_summary",
            description = "Get total sales amount, invoice count and average bill value for a date range, optionally scoped to a company, store group or store. Use this for questions like 'how were sales last week' or 'what did store X sell yesterday'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    companyId = new { type = "string", description = "Optional company GUID to filter by." },
                    storeGroupId = new { type = "string", description = "Optional store group GUID to filter by." },
                    storeId = new { type = "string", description = "Optional store GUID to filter by." },
                    fromDate = new { type = "string", description = "Start date, format yyyy-MM-dd. Defaults to the start of the current month." },
                    toDate = new { type = "string", description = "End date (inclusive), format yyyy-MM-dd. Defaults to today." }
                },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "compare_store_performance",
            description = "Compare sales, purchases and stock value across stores for a date range, ranked highest sales first. Use this for 'which store is doing best' or 'compare store A and store B'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    companyId = new { type = "string", description = "Optional company GUID to filter by." },
                    storeGroupId = new { type = "string", description = "Optional store group GUID to filter by." },
                    fromDate = new { type = "string", description = "Start date, format yyyy-MM-dd. Defaults to the start of the current month." },
                    toDate = new { type = "string", description = "End date (inclusive), format yyyy-MM-dd. Defaults to today." },
                    top = new { type = "integer", description = "Maximum number of stores to return, default 10." }
                },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "get_low_stock_items",
            description = "List on-book products at or below a quantity threshold, ordered lowest stock first. Use this for 'what's running low' or 'what needs reordering'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    companyId = new { type = "string", description = "Optional company GUID to filter by." },
                    storeGroupId = new { type = "string", description = "Optional store group GUID to filter by." },
                    storeId = new { type = "string", description = "Optional store GUID to filter by." },
                    thresholdQty = new { type = "number", description = "Quantity at or below which an item is considered low stock. Default 3." },
                    take = new { type = "integer", description = "Maximum number of rows to return, default 20." }
                },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "get_outstanding_dues",
            description = "List outstanding customer receivables or vendor payables (bills with an amount still unpaid), largest due first. Use this for 'who owes us money' or 'which vendors are we due to pay'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    partyType = new { type = "string", @enum = new[] { "customer", "vendor" }, description = "Whether to list customer receivables or vendor payables." },
                    companyId = new { type = "string", description = "Optional company GUID to filter by." },
                    storeId = new { type = "string", description = "Optional store GUID to filter by (customer dues only)." },
                    take = new { type = "integer", description = "Maximum number of rows to return, default 10." }
                },
                required = new[] { "partyType" }
            }
        },
        new
        {
            name = "get_business_snapshot",
            description = "Get a broad business snapshot for a date range: key metrics, top stores and store groups, customer/vendor dues grouped by age, cash payment summary and health warnings. Use this for open-ended questions like 'how is the business doing' or 'give me an overview'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    companyId = new { type = "string", description = "Optional company GUID to filter by." },
                    storeGroupId = new { type = "string", description = "Optional store group GUID to filter by." },
                    storeId = new { type = "string", description = "Optional store GUID to filter by." },
                    fromDate = new { type = "string", description = "Start date, format yyyy-MM-dd. Defaults to the start of the current month." },
                    toDate = new { type = "string", description = "End date inclusive, format yyyy-MM-dd. Defaults to today." }
                },
                required = Array.Empty<string>()
            }
        },
        new
        {
            name = "get_today_snapshot",
            description = "Get today's snapshot: cash flow, staff attendance, absent employees and quick action items. Use this for 'how is today going', 'who is absent today' or 'what is our cash position today'.",
            input_schema = new
            {
                type = "object",
                properties = new
                {
                    companyId = new { type = "string", description = "Optional company GUID to filter by." },
                    storeGroupId = new { type = "string", description = "Optional store group GUID to filter by." },
                    storeId = new { type = "string", description = "Optional store GUID to filter by." },
                    date = new { type = "string", description = "Date, format yyyy-MM-dd. Defaults to today." }
                },
                required = Array.Empty<string>()
            }
        }
    ];

    public async Task<string> ExecuteAsync(string toolName, string inputJson, HttpContext context, CancellationToken cancellationToken)
    {
        using var input = string.IsNullOrWhiteSpace(inputJson) ? JsonDocument.Parse("{}") : JsonDocument.Parse(inputJson);
        var root = input.RootElement;

        return toolName switch
        {
            "get_store_sales_summary" => await GetStoreSalesSummaryAsync(root, context, cancellationToken),
            "compare_store_performance" => await CompareStorePerformanceAsync(root, context, cancellationToken),
            "get_low_stock_items" => await GetLowStockItemsAsync(root, context, cancellationToken),
            "get_outstanding_dues" => await GetOutstandingDuesAsync(root, context, cancellationToken),
            "get_business_snapshot" => await GetBusinessSnapshotAsync(root, context, cancellationToken),
            "get_today_snapshot" => await GetTodaySnapshotAsync(root, context, cancellationToken),
            _ => JsonSerializer.Serialize(new { error = $"Unknown tool '{toolName}'." })
        };
    }

    private async Task<string> GetBusinessSnapshotAsync(JsonElement input, HttpContext context, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(input);
        var companyId = ReadGuid(input, "companyId");
        var storeGroupId = ReadGuid(input, "storeGroupId");
        var storeId = ReadGuid(input, "storeId");

        var dashboard = await DashboardEndpoints.BusinessAsync(
            context,
            db,
            companyId,
            storeGroupId,
            storeId,
            from,
            to.AddDays(-1),
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            dashboard.Scope,
            dashboard.Period,
            metrics = dashboard.Metrics.Select(item => new { item.Label, item.DisplayValue, item.Caption }),
            topStores = dashboard.Stores
                .OrderByDescending(item => item.SalesMonth)
                .Take(10)
                .Select(item => new { item.StoreName, item.SalesMonth, item.PurchaseMonth, item.StockValue, item.InvoiceCount, item.CurrentStockQty }),
            topStoreGroups = dashboard.StoreGroups
                .OrderByDescending(item => item.SalesMonth)
                .Take(10)
                .Select(item => new { item.StoreGroupName, item.StoreCount, item.SalesMonth, item.PurchaseMonth, item.StockValue, item.InvoiceCount, item.CurrentStockQty }),
            dashboard.CashPaymentSummary,
            customerDuesByAge = dashboard.CustomerDues
                .GroupBy(item => item.AgeBucket)
                .Select(group => new { ageBucket = group.Key, count = group.Count(), dueAmount = group.Sum(item => item.DueAmount) }),
            vendorDuesByAge = dashboard.VendorDues
                .GroupBy(item => item.AgeBucket)
                .Select(group => new { ageBucket = group.Key, count = group.Count(), dueAmount = group.Sum(item => item.DueAmount) }),
            dashboard.HealthSignals
        });
    }

    private async Task<string> GetTodaySnapshotAsync(JsonElement input, HttpContext context, CancellationToken cancellationToken)
    {
        var companyId = ReadGuid(input, "companyId");
        var storeGroupId = ReadGuid(input, "storeGroupId");
        var storeId = ReadGuid(input, "storeId");
        var date = ReadDate(input, "date");

        var today = await DashboardEndpoints.TodaysAsync(
            context,
            db,
            companyId,
            storeGroupId,
            storeId,
            date,
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            today.Scope,
            businessDate = today.BusinessDate.ToString("yyyy-MM-dd"),
            metrics = today.Metrics.Select(item => new { item.Label, item.DisplayValue, item.Caption }),
            today.CashFlow,
            attendance = new
            {
                today.Attendance.ActiveEmployees,
                today.Attendance.PresentEmployees,
                today.Attendance.AbsentEmployees,
                today.Attendance.PendingReviewEmployees,
                absentEmployees = today.Attendance.Absent
                    .Take(25)
                    .Select(item => new { item.EmployeeCode, item.EmployeeName, item.Department, item.Designation })
            },
            today.QuickActions
        });
    }

    private async Task<string> GetStoreSalesSummaryAsync(JsonElement input, HttpContext context, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(input);
        var companyId = ReadGuid(input, "companyId");
        var storeGroupId = ReadGuid(input, "storeGroupId");
        var storeId = ReadGuid(input, "storeId");

        var sales = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.OnDate >= from && item.OnDate < to);

        if (companyId.HasValue) sales = sales.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) sales = sales.Where(item => StoreIdsForGroup(context, storeGroupId.Value).Contains(item.StoreId));
        if (storeId.HasValue) sales = sales.Where(item => item.StoreId == storeId.Value);

        var totalAmount = await SumAsync(sales.Select(item => item.BillAmount), cancellationToken);
        var paidAmount = await SumAsync(sales.Select(item => item.PaidAmount), cancellationToken);
        var invoiceCount = await sales.CountAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            fromDate = from.ToString("yyyy-MM-dd"),
            toDate = to.AddDays(-1).ToString("yyyy-MM-dd"),
            totalSalesAmount = totalAmount,
            totalCollectedAmount = paidAmount,
            outstandingAmount = totalAmount - paidAmount,
            invoiceCount,
            averageBillValue = invoiceCount == 0 ? 0 : Math.Round(totalAmount / invoiceCount, 2)
        });
    }

    private async Task<string> CompareStorePerformanceAsync(JsonElement input, HttpContext context, CancellationToken cancellationToken)
    {
        var (from, to) = ResolveRange(input);
        var companyId = ReadGuid(input, "companyId");
        var storeGroupId = ReadGuid(input, "storeGroupId");
        var top = ReadInt(input, "top") ?? 10;

        var stores = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context);
        if (companyId.HasValue) stores = stores.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) stores = stores.Where(item => item.StoreGroupId == storeGroupId.Value);

        var storeRows = await stores.OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);

        var results = new List<StoreComparisonRow>();
        foreach (var store in storeRows)
        {
            var sales = await SumAsync(db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= from && item.OnDate < to)
                .Select(item => item.BillAmount), cancellationToken);
            var purchases = await SumAsync(db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= from && item.OnDate < to)
                .Select(item => item.BillAmount), cancellationToken);
            var stockValue = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && !item.IsOFB && item.StoreId == store.Id)
                .Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
            var invoiceCount = await db.SalesInvoices.AsNoTracking()
                .CountAsync(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= from && item.OnDate < to, cancellationToken);

            results.Add(new StoreComparisonRow(store.Id, store.Name, sales, purchases, stockValue, invoiceCount));
        }

        var ordered = results
            .OrderByDescending(item => item.SalesAmount)
            .Take(Math.Clamp(top, 1, 50))
            .Select(item => new
            {
                storeId = item.StoreId,
                storeName = item.StoreName,
                salesAmount = item.SalesAmount,
                purchaseAmount = item.PurchaseAmount,
                stockValue = item.StockValue,
                invoiceCount = item.InvoiceCount
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            fromDate = from.ToString("yyyy-MM-dd"),
            toDate = to.AddDays(-1).ToString("yyyy-MM-dd"),
            storeCount = storeRows.Count,
            stores = ordered
        });
    }

    private async Task<string> GetLowStockItemsAsync(JsonElement input, HttpContext context, CancellationToken cancellationToken)
    {
        var companyId = ReadGuid(input, "companyId");
        var storeGroupId = ReadGuid(input, "storeGroupId");
        var storeId = ReadGuid(input, "storeId");
        var threshold = ReadDecimal(input, "thresholdQty") ?? 3m;
        var take = ReadInt(input, "take") ?? 20;

        var stocks = WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking().Include(item => item.Product), context)
            .Where(item => !item.Deleted && !item.IsOFB && (item.PurchaseQty - item.SoldQty) <= threshold);

        if (companyId.HasValue) stocks = stocks.Where(item => item.CompanyId == companyId.Value);
        if (storeGroupId.HasValue) stocks = stocks.Where(item => item.StoreGroupId == storeGroupId.Value);
        if (storeId.HasValue) stocks = stocks.Where(item => item.StoreId == storeId.Value);

        var rows = await stocks
            .OrderBy(item => item.PurchaseQty - item.SoldQty)
            .Take(Math.Clamp(take, 1, 100))
            .Select(item => new
            {
                productName = item.Product != null ? item.Product.Name : "(unknown product)",
                barcode = item.Barcode,
                storeId = item.StoreId,
                currentQty = item.PurchaseQty - item.SoldQty,
                costPrice = item.CostPrice
            })
            .ToListAsync(cancellationToken);

        return JsonSerializer.Serialize(new { thresholdQty = threshold, matchCount = rows.Count, items = rows });
    }

    private async Task<string> GetOutstandingDuesAsync(JsonElement input, HttpContext context, CancellationToken cancellationToken)
    {
        var partyType = ReadString(input, "partyType")?.ToLowerInvariant();
        var companyId = ReadGuid(input, "companyId");
        var storeId = ReadGuid(input, "storeId");
        var take = Math.Clamp(ReadInt(input, "take") ?? 10, 1, 50);

        if (partyType == "vendor")
        {
            var purchases = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.BillAmount > 0);
            if (companyId.HasValue) purchases = purchases.Where(item => item.CompanyId == companyId.Value);

            var invoiceRows = await purchases
                .Select(item => new { item.Id, item.VendorId, item.VendorName, item.BillAmount })
                .ToListAsync(cancellationToken);
            var invoiceIds = invoiceRows.Select(item => item.Id).ToList();
            var payments = invoiceIds.Count == 0
                ? new Dictionary<Guid, decimal>()
                : await db.PurchasePayments.AsNoTracking()
                    .Where(item => invoiceIds.Contains(item.PurchaseInvoiceId) && !item.Deleted)
                    .GroupBy(item => item.PurchaseInvoiceId)
                    .Select(group => new { PurchaseInvoiceId = group.Key, Paid = group.Sum(item => item.Amount) })
                    .ToDictionaryAsync(item => item.PurchaseInvoiceId, item => item.Paid, cancellationToken);

            var vendorDues = invoiceRows
                .Select(item => new { item.VendorId, item.VendorName, item.BillAmount, Paid = payments.TryGetValue(item.Id, out var paid) ? paid : 0m })
                .Where(item => item.BillAmount > item.Paid)
                .GroupBy(item => new { item.VendorId, item.VendorName })
                .Select(group => new
                {
                    vendorId = group.Key.VendorId,
                    vendorName = group.Key.VendorName ?? "Vendor",
                    billCount = group.Count(),
                    dueAmount = group.Sum(item => item.BillAmount - item.Paid)
                })
                .OrderByDescending(item => item.dueAmount)
                .Take(take)
                .ToList();

            return JsonSerializer.Serialize(new { partyType = "vendor", rowCount = vendorDues.Count, rows = vendorDues });
        }
        else
        {
            var sales = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.BillAmount > item.PaidAmount);
            if (companyId.HasValue) sales = sales.Where(item => item.CompanyId == companyId.Value);
            if (storeId.HasValue) sales = sales.Where(item => item.StoreId == storeId.Value);

            var customerDues = await sales
                .GroupBy(item => new { item.CustomerId, item.CustomerName, item.CustomerMobileNumber })
                .Select(group => new
                {
                    customerId = group.Key.CustomerId,
                    customerName = group.Key.CustomerName ?? group.Key.CustomerMobileNumber ?? "Walk-in customer",
                    billCount = group.Count(),
                    dueAmount = group.Sum(item => item.BillAmount - item.PaidAmount)
                })
                .OrderByDescending(item => item.dueAmount)
                .Take(take)
                .ToListAsync(cancellationToken);

            return JsonSerializer.Serialize(new { partyType = "customer", rowCount = customerDues.Count, rows = customerDues });
        }
    }

    private static (DateTime From, DateTime ToExclusive) ResolveRange(JsonElement input)
    {
        var today = DateTime.Today;
        var defaultFrom = new DateTime(today.Year, today.Month, 1);
        var from = ReadDate(input, "fromDate") ?? defaultFrom;
        var to = ReadDate(input, "toDate") ?? today;
        if (to < from) to = from;
        return (from.Date, to.Date.AddDays(1));
    }

    private static async Task<decimal> SumAsync(IQueryable<decimal> values, CancellationToken cancellationToken)
        => await values.Select(item => (decimal?)item).SumAsync(cancellationToken) ?? 0m;

    private static string? ReadString(JsonElement input, string name)
        => input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Guid? ReadGuid(JsonElement input, string name)
        => Guid.TryParse(ReadString(input, name), out var guid) ? guid : null;

    private static DateTime? ReadDate(JsonElement input, string name)
        => DateTime.TryParse(ReadString(input, name), out var date) ? date : null;

    private static int? ReadInt(JsonElement input, string name)
    {
        if (!input.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(ReadString(input, name), out var parsed) ? parsed : null;
    }

    private static decimal? ReadDecimal(JsonElement input, string name)
    {
        if (!input.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return decimal.TryParse(ReadString(input, name), out var parsed) ? parsed : null;
    }

    private IQueryable<Guid> StoreIdsForGroup(HttpContext context, Guid storeGroupId)
        => WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
            .Where(item => item.StoreGroupId == storeGroupId)
            .Select(item => item.Id);

    private sealed record StoreComparisonRow(
        Guid StoreId,
        string StoreName,
        decimal SalesAmount,
        decimal PurchaseAmount,
        decimal StockValue,
        int InvoiceCount);
}
