using Garmetix.Api.Workspace;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Dashboard;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization();

        group.MapGet("/home", HomeAsync).WithName("GetDashboardHome");
        group.MapGet("/store-manager", StoreManagerAsync).WithName("GetStoreManagerDashboard");
        group.MapGet("/business", BusinessAsync).WithName("GetBusinessDashboard");

        return group;
    }


    private static Task<DashboardHomeDto> HomeAsync(HttpContext context)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userType = context.User.FindFirst("userType")?.Value ?? string.Empty;
        var combined = $"{role} {userType}".ToLowerInvariant();
        var canOpenBusiness = WorkspaceScope.HasFullAccess(context)
            || combined.Contains("admin")
            || combined.Contains("owner")
            || combined.Contains("accountant");
        var route = canOpenBusiness ? "/dashboard/business" : "/dashboard/store-manager";
        var dashboardType = canOpenBusiness ? "Business" : "StoreManager";
        var reason = canOpenBusiness
            ? "Owner, admin and accountant users start with company/store-group dashboard."
            : "Store scoped users start with the store manager dashboard.";

        return Task.FromResult(new DashboardHomeDto(route, dashboardType, reason, canOpenBusiness, true));
    }

    private static async Task<StoreManagerDashboardDto> StoreManagerAsync(
        HttpContext context,
        GarmetixDbContext db,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? storeGroupId,
        [FromQuery] Guid? storeId,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var rangeStart = today.AddDays(-6);

        var sales = FilterSales(WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context), companyId, storeId)
            .Where(item => !item.ReturnInvoice);
        var purchases = FilterPurchases(WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => !item.ReturnInvoice);
        var stocks = FilterStocks(WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking().Include(item => item.Product), context), companyId, storeGroupId, storeId);

        var salesToday = await SumAsync(sales.Where(item => item.OnDate >= today && item.OnDate < tomorrow).Select(item => item.BillAmount), cancellationToken);
        var salesMonth = await SumAsync(sales.Where(item => item.OnDate >= monthStart).Select(item => item.BillAmount), cancellationToken);
        var purchaseMonth = await SumAsync(purchases.Where(item => item.OnDate >= monthStart).Select(item => item.BillAmount), cancellationToken);
        var stockValue = await SumAsync(stocks.Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
        var lowStockCount = await stocks.CountAsync(item => (item.PurchaseQty - item.SoldQty) <= 3, cancellationToken);
        var todayInvoiceCount = await sales.CountAsync(item => item.OnDate >= today && item.OnDate < tomorrow, cancellationToken);
        var dueInvoiceCount = await sales.CountAsync(item => item.BillAmount > item.PaidAmount, cancellationToken);
        var activeStockCount = await stocks.CountAsync(item => (item.PurchaseQty - item.SoldQty) > 0, cancellationToken);
        var nonGstSalesMonth = await SumAsync(FilterNonGst(WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Sale && item.OnDate >= monthStart)
            .Select(item => item.NetAmount), cancellationToken);
        var nonGstPurchaseMonth = await SumAsync(FilterNonGst(WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Purchase && item.OnDate >= monthStart)
            .Select(item => item.NetAmount), cancellationToken);

        var trend = await TrendAsync(sales, purchases, rangeStart, today, cancellationToken);
        var scope = await ResolveScopeAsync(context, db, companyId, storeGroupId, storeId, "Current store", cancellationToken);

        var recentSales = await sales
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(8)
            .Select(item => new DashboardActivityDto(
                item.InvoiceNumber,
                item.CustomerName ?? item.CustomerMobileNumber ?? "Walk-in customer",
                FormatMoney(item.BillAmount),
                item.OnDate,
                item.InvoiceStatus.ToString(),
                "billing",
                item.Id))
            .ToListAsync(cancellationToken);

        var stockAlerts = await stocks
            .Where(item => (item.PurchaseQty - item.SoldQty) <= 3)
            .OrderBy(item => item.Product!.Name)
            .ThenBy(item => item.Barcode)
            .Take(8)
            .Select(item => new DashboardActivityDto(
                item.Product!.Name,
                item.Barcode,
                (item.PurchaseQty - item.SoldQty).ToString("0.##"),
                item.UpdatedAt ?? item.CreatedAt,
                "Low Stock",
                "inventory",
                item.Id))
            .ToListAsync(cancellationToken);

        var workQueue = new List<DashboardActivityDto>
        {
            new("Open billing", "Create store cash memo / invoice", string.Empty, DateTime.Now, "Action", "billing", null),
            new("Review low stock", $"{lowStockCount} item(s) need attention", string.Empty, DateTime.Now, lowStockCount > 0 ? "Required" : "Ready", "inventory", null),
            new("Daily reports", "Check sales, stock, and petty cash", string.Empty, DateTime.Now, "Routine", "reports", null),
            new("Non-GST cash memo", "Record legally out-of-scope goods separately", string.Empty, DateTime.Now, "Separate", "non-gst-goods", null)
        };

        var quickActions = new List<DashboardQuickActionDto>
        {
            new("New sale", "Open billing for current store", "/billing", "i-lucide-receipt-indian-rupee", "primary", false),
            new("Purchase inward", "Add new purchase stock", "/purchase", "i-lucide-package-plus", "warning", false),
            new("Stock operations", "Adjust, transfer or count stock", "/stock-operations", "i-lucide-arrow-left-right", "neutral", lowStockCount > 0),
            new("Non-GST memo", "Separate out-of-scope purchase/sale register", "/non-gst-goods", "i-lucide-file-warning", "neutral", false)
        };

        var healthSignals = new List<DashboardHealthSignalDto>
        {
            new("Low stock", lowStockCount.ToString(), lowStockCount > 0 ? "Needs attention" : "Healthy", "Items at or below qty 3.", "i-lucide-triangle-alert", lowStockCount > 0 ? "warning" : "success"),
            new("Due invoices", dueInvoiceCount.ToString(), dueInvoiceCount > 0 ? "Collect" : "Clear", "Bills where paid amount is below bill amount.", "i-lucide-wallet-cards", dueInvoiceCount > 0 ? "warning" : "success"),
            new("Active stock", activeStockCount.ToString(), "In stock", "Stock rows with positive current quantity.", "i-lucide-boxes", "neutral"),
            new("Non-GST month", FormatMoney(nonGstSalesMonth - nonGstPurchaseMonth), "Separate register", "Non-GST sale minus purchase, excluded from GST return reports.", "i-lucide-file-warning", "primary")
        };

        return new StoreManagerDashboardDto(
            scope,
            [
                Metric("Today Sales", salesToday, "Current store collection", "i-lucide-receipt-indian-rupee", "success"),
                Metric("Month Sales", salesMonth, "Current month billing", "i-lucide-trending-up", "primary"),
                Metric("Month Purchase", purchaseMonth, "Current month inward", "i-lucide-package-plus", "warning"),
                Metric("Stock Value", stockValue, "Available stock cost", "i-lucide-boxes", "neutral"),
                Metric("Invoices Today", todayInvoiceCount, "Bills created today", "i-lucide-file-check-2", "primary"),
                Metric("Low Stock", lowStockCount, "Qty at or below 3", "i-lucide-triangle-alert", lowStockCount > 0 ? "warning" : "success")
            ],
            trend,
            recentSales,
            stockAlerts,
            workQueue,
            quickActions,
            healthSignals);
    }

    private static async Task<BusinessDashboardDto> BusinessAsync(
        HttpContext context,
        GarmetixDbContext db,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? storeGroupId,
        [FromQuery] Guid? storeId,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var rangeStart = today.AddDays(-6);

        var sales = FilterSales(WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context), companyId, storeId)
            .Where(item => !item.ReturnInvoice);
        var purchases = FilterPurchases(WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => !item.ReturnInvoice);
        var stocks = FilterStocks(WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context), companyId, storeGroupId, storeId);
        var customers = FilterCompany(WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context), companyId);
        var vendors = FilterCompany(WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context), companyId);

        var salesMonth = await SumAsync(sales.Where(item => item.OnDate >= monthStart).Select(item => item.BillAmount), cancellationToken);
        var purchaseMonth = await SumAsync(purchases.Where(item => item.OnDate >= monthStart).Select(item => item.BillAmount), cancellationToken);
        var stockValue = await SumAsync(stocks.Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
        var grossMargin = salesMonth - purchaseMonth;
        var customerCount = await customers.CountAsync(cancellationToken);
        var vendorCount = await vendors.CountAsync(cancellationToken);
        var invoiceCount = await sales.Where(item => item.OnDate >= monthStart).CountAsync(cancellationToken);
        var dueInvoiceCount = await sales.CountAsync(item => item.BillAmount > item.PaidAmount, cancellationToken);
        var lowStockCount = await stocks.CountAsync(item => (item.PurchaseQty - item.SoldQty) <= 3, cancellationToken);
        var nonGstSalesMonth = await SumAsync(FilterNonGst(WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Sale && item.OnDate >= monthStart)
            .Select(item => item.NetAmount), cancellationToken);
        var nonGstPurchaseMonth = await SumAsync(FilterNonGst(WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Purchase && item.OnDate >= monthStart)
            .Select(item => item.NetAmount), cancellationToken);

        var trend = await TrendAsync(sales, purchases, rangeStart, today, cancellationToken);
        var scope = await ResolveScopeAsync(context, db, companyId, storeGroupId, storeId, "Company / store group", cancellationToken);
        var storeRows = await StorePerformanceAsync(context, db, companyId, storeGroupId, storeId, monthStart, cancellationToken);
        var storeGroupRows = await StoreGroupPerformanceAsync(context, db, companyId, storeGroupId, storeId, monthStart, cancellationToken);

        var recentSales = await sales
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(8)
            .Select(item => new DashboardActivityDto(
                item.InvoiceNumber,
                item.CustomerName ?? item.CustomerMobileNumber ?? "Walk-in customer",
                FormatMoney(item.BillAmount),
                item.OnDate,
                item.InvoiceStatus.ToString(),
                "billing",
                item.Id))
            .ToListAsync(cancellationToken);

        var recentPurchases = await purchases
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(8)
            .Select(item => new DashboardActivityDto(
                item.InvoiceNumber,
                item.VendorName ?? "Vendor",
                FormatMoney(item.BillAmount),
                item.OnDate,
                item.InvoiceStatus.ToString(),
                "purchase",
                item.Id))
            .ToListAsync(cancellationToken);

        var adminQueue = new List<DashboardActivityDto>
        {
            new("Company performance", "Review store-wise sales, purchase, stock and margin", string.Empty, DateTime.Now, "Dashboard", "dashboard/business", null),
            new("GST reports", "Review GST before return filing", string.Empty, DateTime.Now, "Report", "gst-reports", null),
            new("Data consistency", "Run summary before production close", string.Empty, DateTime.Now, "Control", "data-consistency", null),
            new("Message logs", "Audit onboarding, seeding and runtime messages", string.Empty, DateTime.Now, "Control", "message-logs", null)
        };

        var quickActions = new List<DashboardQuickActionDto>
        {
            new("Business dashboard", "Review group/store KPIs", "/dashboard/business", "i-lucide-chart-no-axes-combined", "primary", false),
            new("GST reports", "Review tax summaries", "/gst-reports", "i-lucide-table-properties", "warning", false),
            new("Message logs", "Search operational events", "/message-logs", "i-lucide-list-collapse", "neutral", false),
            new("Consistency repair", "Run controlled checks before close", "/data-consistency", "i-lucide-shield-alert", "neutral", lowStockCount > 0 || dueInvoiceCount > 0)
        };

        var healthSignals = new List<DashboardHealthSignalDto>
        {
            new("Due invoices", dueInvoiceCount.ToString(), dueInvoiceCount > 0 ? "Collect" : "Clear", "Bills where paid amount is below bill amount.", "i-lucide-wallet-cards", dueInvoiceCount > 0 ? "warning" : "success"),
            new("Low stock", lowStockCount.ToString(), lowStockCount > 0 ? "Review" : "Healthy", "Items at or below qty 3 across permitted stores.", "i-lucide-triangle-alert", lowStockCount > 0 ? "warning" : "success"),
            new("Non-GST margin", FormatMoney(nonGstSalesMonth - nonGstPurchaseMonth), "Separate register", "Out-of-scope goods result; excluded from GST return reports.", "i-lucide-file-warning", "primary"),
            new("Vendors", vendorCount.ToString(), "Active master", $"{customerCount} customers available for business operations.", "i-lucide-users", "neutral")
        };

        return new BusinessDashboardDto(
            scope,
            [
                Metric("Month Sales", salesMonth, "All permitted stores", "i-lucide-trending-up", "success"),
                Metric("Month Purchase", purchaseMonth, "All permitted stores", "i-lucide-package-plus", "warning"),
                Metric("Gross Margin", grossMargin, "Sales minus purchase", "i-lucide-chart-no-axes-combined", grossMargin >= 0 ? "primary" : "error"),
                Metric("Stock Value", stockValue, "Available stock cost", "i-lucide-boxes", "neutral"),
                Metric("Invoices", invoiceCount, "Current month bills", "i-lucide-file-check-2", "primary"),
                Metric("Customers", customerCount, $"{vendorCount} vendors", "i-lucide-users", "neutral")
            ],
            trend,
            storeRows,
            storeGroupRows,
            recentSales,
            recentPurchases,
            adminQueue,
            quickActions,
            healthSignals);
    }

    private static IQueryable<Garmetix.Core.Models.Inventory.Invoice> FilterSales(
        IQueryable<Garmetix.Core.Models.Inventory.Invoice> query,
        Guid? companyId,
        Guid? storeId)
    {
        if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return query;
    }

    private static IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> FilterPurchases(
        IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> query,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId)
    {
        if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return query;
    }

    private static IQueryable<Garmetix.Core.Models.Inventory.Stock> FilterStocks(
        IQueryable<Garmetix.Core.Models.Inventory.Stock> query,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId)
    {
        if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return query;
    }

    private static IQueryable<Garmetix.Core.Models.Inventory.NonGstGoodsDocument> FilterNonGst(
        IQueryable<Garmetix.Core.Models.Inventory.NonGstGoodsDocument> query,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId)
    {
        if (companyId.HasValue)
        {
            query = query.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            query = query.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        if (storeId.HasValue)
        {
            query = query.Where(item => item.StoreId == storeId.Value);
        }

        return query;
    }

    private static IQueryable<T> FilterCompany<T>(IQueryable<T> query, Guid? companyId)
        where T : Garmetix.Core.Models.Base.CompanyBase
    {
        return companyId.HasValue ? query.Where(item => item.CompanyId == companyId.Value) : query;
    }

    private static async Task<IReadOnlyList<StorePerformanceDto>> StorePerformanceAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime monthStart,
        CancellationToken cancellationToken)
    {
        var stores = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context);
        if (companyId.HasValue)
        {
            stores = stores.Where(item => item.CompanyId == companyId.Value);
        }
        if (storeGroupId.HasValue)
        {
            stores = stores.Where(item => item.StoreGroupId == storeGroupId.Value);
        }
        if (storeId.HasValue)
        {
            stores = stores.Where(item => item.Id == storeId.Value);
        }

        var rows = await stores
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        var result = new List<StorePerformanceDto>();
        foreach (var store in rows)
        {
            var sales = await SumAsync(db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= monthStart)
                .Select(item => item.BillAmount), cancellationToken);
            var purchases = await SumAsync(db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= monthStart)
                .Select(item => item.BillAmount), cancellationToken);
            var stockValue = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && item.StoreId == store.Id)
                .Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
            var currentStock = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && item.StoreId == store.Id)
                .Select(item => item.PurchaseQty - item.SoldQty), cancellationToken);
            var invoiceCount = await db.SalesInvoices.AsNoTracking()
                .CountAsync(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= monthStart, cancellationToken);

            result.Add(new StorePerformanceDto(store.Id, store.Name, sales, purchases, stockValue, invoiceCount, currentStock));
        }

        return result;
    }

    private static async Task<IReadOnlyList<StoreGroupPerformanceDto>> StoreGroupPerformanceAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime monthStart,
        CancellationToken cancellationToken)
    {
        var groups = WorkspaceScope.ApplyTo(db.StoreGroups.AsNoTracking(), context);
        if (companyId.HasValue)
        {
            groups = groups.Where(item => item.CompanyId == companyId.Value);
        }
        if (storeGroupId.HasValue)
        {
            groups = groups.Where(item => item.Id == storeGroupId.Value);
        }

        var rows = await groups
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        var result = new List<StoreGroupPerformanceDto>();
        foreach (var group in rows)
        {
            var stores = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context)
                .Where(item => item.StoreGroupId == group.Id);
            if (storeId.HasValue)
            {
                stores = stores.Where(item => item.Id == storeId.Value);
            }

            var storeIds = await stores.Select(item => item.Id).ToListAsync(cancellationToken);
            if (storeIds.Count == 0)
            {
                result.Add(new StoreGroupPerformanceDto(group.Id, group.Name, 0, 0, 0, 0, 0, 0));
                continue;
            }

            var sales = await SumAsync(db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && storeIds.Contains(item.StoreId) && item.OnDate >= monthStart)
                .Select(item => item.BillAmount), cancellationToken);
            var purchases = await SumAsync(db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId.HasValue && storeIds.Contains(item.StoreId.Value) && item.OnDate >= monthStart)
                .Select(item => item.BillAmount), cancellationToken);
            var stockValue = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && storeIds.Contains(item.StoreId))
                .Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
            var currentStock = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && storeIds.Contains(item.StoreId))
                .Select(item => item.PurchaseQty - item.SoldQty), cancellationToken);
            var invoiceCount = await db.SalesInvoices.AsNoTracking()
                .CountAsync(item => !item.Deleted && !item.ReturnInvoice && storeIds.Contains(item.StoreId) && item.OnDate >= monthStart, cancellationToken);

            result.Add(new StoreGroupPerformanceDto(group.Id, group.Name, storeIds.Count, sales, purchases, stockValue, invoiceCount, currentStock));
        }

        return result;
    }

    private static async Task<IReadOnlyList<DashboardTrendPointDto>> TrendAsync(
        IQueryable<Garmetix.Core.Models.Inventory.Invoice> sales,
        IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> purchases,
        DateTime rangeStart,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var salesRows = await sales
            .Where(item => item.OnDate >= rangeStart)
            .Select(item => new { item.OnDate, item.BillAmount })
            .ToListAsync(cancellationToken);
        var purchaseRows = await purchases
            .Where(item => item.OnDate >= rangeStart)
            .Select(item => new { item.OnDate, item.BillAmount })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, 7)
            .Select(offset => rangeStart.Date.AddDays(offset))
            .Select(date => new DashboardTrendPointDto(
                date.ToString("dd MMM"),
                date,
                salesRows.Where(item => item.OnDate.Date == date).Sum(item => item.BillAmount),
                purchaseRows.Where(item => item.OnDate.Date == date).Sum(item => item.BillAmount)))
            .ToList();
    }

    private static async Task<DashboardScopeDto> ResolveScopeAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string defaultScope,
        CancellationToken cancellationToken)
    {
        var stores = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context);
        var companies = WorkspaceScope.ApplyTo(db.Companies.AsNoTracking(), context);
        var groups = WorkspaceScope.ApplyTo(db.StoreGroups.AsNoTracking(), context);

        var store = storeId.HasValue
            ? await stores.FirstOrDefaultAsync(item => item.Id == storeId.Value, cancellationToken)
            : await stores.OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        var groupId = storeGroupId ?? store?.StoreGroupId;
        var company = companyId.HasValue
            ? await companies.FirstOrDefaultAsync(item => item.Id == companyId.Value, cancellationToken)
            : store is null ? await companies.OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken) : await companies.FirstOrDefaultAsync(item => item.Id == store.CompanyId, cancellationToken);
        var group = groupId.HasValue
            ? await groups.FirstOrDefaultAsync(item => item.Id == groupId.Value, cancellationToken)
            : null;

        return new DashboardScopeDto(
            defaultScope,
            company?.Id ?? companyId,
            group?.Id ?? storeGroupId,
            store?.Id ?? storeId,
            company?.Name ?? "All permitted companies",
            group?.Name ?? "All permitted store groups",
            store?.Name ?? "All permitted stores");
    }

    private static async Task<decimal> SumAsync(IQueryable<decimal> values, CancellationToken cancellationToken)
    {
        return await values.Select(item => (decimal?)item).SumAsync(cancellationToken) ?? 0m;
    }

    private static DashboardMetricDto Metric(string label, decimal value, string caption, string icon, string color)
    {
        return new DashboardMetricDto(label, value, FormatMetric(value), caption, icon, color);
    }

    private static string FormatMetric(decimal value)
    {
        return Math.Abs(value) >= 1000m ? FormatMoney(value) : value.ToString("0.##");
    }

    private static string FormatMoney(decimal value)
    {
        var sign = value < 0 ? "-" : string.Empty;
        value = Math.Abs(value);
        if (value >= 10000000m)
        {
            return $"{sign}₹{value / 10000000m:0.##}Cr";
        }
        if (value >= 100000m)
        {
            return $"{sign}₹{value / 100000m:0.##}L";
        }
        if (value >= 1000m)
        {
            return $"{sign}₹{value / 1000m:0.##}K";
        }
        return $"{sign}₹{value:0}";
    }
}
