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
        group.MapGet("/todays", TodaysAsync).WithName("GetTodaysDashboard");
        group.MapGet("/store-manager", StoreManagerAsync).WithName("GetStoreManagerDashboard");
        group.MapGet("/business", BusinessAsync).WithName("GetBusinessDashboard");

        return group;
    }


    private static Task<IResult> HomeAsync(HttpContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IResult>(Results.Ok(ResolveHome(context.User)));
    }

    public static DashboardHomeDto ResolveHome(ClaimsPrincipal user)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userType = user.FindFirst("userType")?.Value ?? string.Empty;
        var combined = $"{role} {userType}".ToLowerInvariant();
        if (string.Equals(role, LoginRole.HR.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return new DashboardHomeDto("/hr", "HR", "HR users start in attendance and employee operations.", false, false);
        }

        if (string.Equals(role, LoginRole.Payroll.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return new DashboardHomeDto("/payroll", "Payroll", "Payroll users start in salary and payslip operations.", false, false);
        }

        var canOpenBusiness = WorkspaceScope.HasFullAccess(user)
            || combined.Contains("admin")
            || combined.Contains("owner")
            || combined.Contains("accountant")
            || combined.Contains("poweruser");
        if (canOpenBusiness)
        {
            return new DashboardHomeDto(
                "/dashboard/business",
                "Business",
                "Owner, admin, power user and accountant users start with company/store-group dashboard.",
                true,
                true);
        }

        var startsInStoreOperations = string.Equals(role, LoginRole.StoreManager.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, LoginRole.Salesman.ToString(), StringComparison.OrdinalIgnoreCase)
            || combined.Contains("storemanager")
            || combined.Contains("salesman")
            || combined.Contains("sales")
            || combined.Contains("biller")
            || combined.Contains("billing");

        if (startsInStoreOperations)
        {
            return new DashboardHomeDto(
                "/store-day",
                "StoreOperations",
                "Store manager and biller users start at Store Operations so the day can be opened before billing.",
                false,
                true);
        }

        return new DashboardHomeDto(
            "/dashboard/store-manager",
            "StoreManager",
            "Scoped users without a specialized home start with the store manager dashboard.",
            false,
            true);
    }


    private static async Task<TodayDashboardDto> TodaysAsync(
        HttpContext context,
        GarmetixDbContext db,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? storeGroupId,
        [FromQuery] Guid? storeId,
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken)
    {
        var businessDate = (date?.Date ?? DateTime.Today);
        var tomorrow = businessDate.AddDays(1);
        var trendStart = businessDate.AddDays(-13);
        var storeIds = await ResolveStoreIdsAsync(context, db, companyId, storeGroupId, storeId, cancellationToken);
        var hasStoreScope = storeIds.Count > 0;

        var sales = WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice);
        var purchases = WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context)
            .Where(item => !item.Deleted && !item.ReturnInvoice);
        var invoicePayments = WorkspaceScope.ApplyTo(db.InvoicePayments.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var purchasePayments = WorkspaceScope.ApplyTo(db.PurchasePayments.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var vouchers = WorkspaceScope.ApplyTo(db.Vouchers.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var cashVouchers = WorkspaceScope.ApplyTo(db.CashVouchers.AsNoTracking(), context)
            .Where(item => !item.Deleted);
        var nonGstDocuments = WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context)
            .Where(item => !item.Deleted);

        if (companyId.HasValue)
        {
            sales = sales.Where(item => item.CompanyId == companyId.Value);
            purchases = purchases.Where(item => item.CompanyId == companyId.Value);
            invoicePayments = invoicePayments.Where(item => item.CompanyId == companyId.Value);
            purchasePayments = purchasePayments.Where(item => item.CompanyId == companyId.Value);
            vouchers = vouchers.Where(item => item.CompanyId == companyId.Value);
            cashVouchers = cashVouchers.Where(item => item.CompanyId == companyId.Value);
            nonGstDocuments = nonGstDocuments.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeGroupId.HasValue)
        {
            purchases = purchases.Where(item => item.StoreGroupId == storeGroupId.Value);
            purchasePayments = purchasePayments.Where(item => item.StoreGroupId == storeGroupId.Value);
            vouchers = vouchers.Where(item => item.StoreGroupId == storeGroupId.Value);
            cashVouchers = cashVouchers.Where(item => item.StoreGroupId == storeGroupId.Value);
            nonGstDocuments = nonGstDocuments.Where(item => item.StoreGroupId == storeGroupId.Value);
        }

        if (hasStoreScope)
        {
            sales = sales.Where(item => storeIds.Contains(item.StoreId));
            purchases = purchases.Where(item => item.StoreId.HasValue && storeIds.Contains(item.StoreId.Value));
            invoicePayments = invoicePayments.Where(item => storeIds.Contains(item.StoreId));
            purchasePayments = purchasePayments.Where(item => storeIds.Contains(item.StoreId));
            vouchers = vouchers.Where(item => storeIds.Contains(item.StoreId));
            cashVouchers = cashVouchers.Where(item => storeIds.Contains(item.StoreId));
            nonGstDocuments = nonGstDocuments.Where(item => storeIds.Contains(item.StoreId));
        }
        else
        {
            sales = sales.Where(_ => false);
            purchases = purchases.Where(_ => false);
            invoicePayments = invoicePayments.Where(_ => false);
            purchasePayments = purchasePayments.Where(_ => false);
            vouchers = vouchers.Where(_ => false);
            cashVouchers = cashVouchers.Where(_ => false);
            nonGstDocuments = nonGstDocuments.Where(_ => false);
        }

        var todaySalesQuery = sales.Where(item => item.OnDate >= businessDate && item.OnDate < tomorrow);
        var todayPurchaseQuery = purchases.Where(item => item.OnDate >= businessDate && item.OnDate < tomorrow);
        var todayInvoicePayments = invoicePayments.Where(item => item.OnDate >= businessDate && item.OnDate < tomorrow);
        var todayPurchasePayments = purchasePayments.Where(item => item.OnDate >= businessDate && item.OnDate < tomorrow);
        var todayVouchers = vouchers.Where(item => item.OnDate >= businessDate && item.OnDate < tomorrow);
        var todayCashVouchers = cashVouchers.Where(item => item.OnDate >= businessDate && item.OnDate < tomorrow);

        var salesAmount = await SumAsync(todaySalesQuery.Select(item => item.BillAmount), cancellationToken);
        var purchaseAmount = await SumAsync(todayPurchaseQuery.Select(item => item.BillAmount), cancellationToken);
        var salesCollectionAmount = await SumAsync(todayInvoicePayments.Select(item => item.Amount), cancellationToken);
        var purchasePaymentAmount = await SumAsync(todayPurchasePayments.Select(item => item.Amount), cancellationToken);
        var voucherReceiptAmount = await SumAsync(todayVouchers.Where(item => item.VoucherType == VoucherType.Receipt).Select(item => item.Amount), cancellationToken);
        var voucherPaymentAmount = await SumAsync(todayVouchers.Where(item => item.VoucherType == VoucherType.Payment).Select(item => item.Amount), cancellationToken);
        var voucherExpenseAmount = await SumAsync(todayVouchers.Where(item => item.VoucherType == VoucherType.Expense).Select(item => item.Amount), cancellationToken);
        var cashVoucherReceiptAmount = await SumAsync(todayCashVouchers.Where(item => item.VoucherType == VoucherType.Receipt).Select(item => item.Amount), cancellationToken);
        var cashVoucherPaymentAmount = await SumAsync(todayCashVouchers.Where(item => item.VoucherType == VoucherType.Payment).Select(item => item.Amount), cancellationToken);
        var cashVoucherExpenseAmount = await SumAsync(todayCashVouchers.Where(item => item.VoucherType == VoucherType.Expense).Select(item => item.Amount), cancellationToken);
        var invoiceCount = await todaySalesQuery.CountAsync(cancellationToken);
        var purchaseCount = await todayPurchaseQuery.CountAsync(cancellationToken);
        var cashVoucherCount = await todayCashVouchers.CountAsync(cancellationToken);

        var totalReceipts = salesCollectionAmount + voucherReceiptAmount + cashVoucherReceiptAmount;
        var totalPayments = purchasePaymentAmount + voucherPaymentAmount + cashVoucherPaymentAmount;
        var totalExpenses = voucherExpenseAmount + cashVoucherExpenseAmount;
        var netCashFlow = totalReceipts - totalPayments - totalExpenses;

        var employeeQuery = WorkspaceScope.ApplyTo(db.Employees.AsNoTracking(), context)
            .Where(item => !item.Deleted && item.Working && (item.EmployeeStatus == null || item.EmployeeStatus == string.Empty || item.EmployeeStatus == "Active"));
        if (companyId.HasValue)
        {
            employeeQuery = employeeQuery.Where(item => item.CompanyId == companyId.Value);
        }
        if (storeGroupId.HasValue)
        {
            employeeQuery = employeeQuery.Where(item => item.StoreGroupId == storeGroupId.Value);
        }
        if (hasStoreScope)
        {
            employeeQuery = employeeQuery.Where(item => storeIds.Contains(item.StoreId));
        }
        else
        {
            employeeQuery = employeeQuery.Where(_ => false);
        }

        var employees = await employeeQuery
            .OrderBy(item => item.FirstName)
            .ThenBy(item => item.LastName)
            .Select(item => new
            {
                item.Id,
                item.EmpId,
                item.EmployeeCode,
                item.FirstName,
                item.LastName,
                item.Department,
                item.Designation
            })
            .ToListAsync(cancellationToken);

        var employeeIds = employees.Select(item => item.Id).ToList();
        var punches = await WorkspaceScope.ApplyTo(db.AttendancePunches.AsNoTracking(), context)
            .Where(item => !item.Deleted && employeeIds.Contains(item.EmployeeId) && item.LocalPunchTime >= businessDate && item.LocalPunchTime < tomorrow)
            .OrderBy(item => item.LocalPunchTime)
            .Select(item => new
            {
                item.EmployeeId,
                item.PunchType,
                item.LocalPunchTime,
                item.Source,
                item.VerificationStatus
            })
            .ToListAsync(cancellationToken);

        var punchGroups = punches.GroupBy(item => item.EmployeeId).ToDictionary(item => item.Key, item => item.ToList());
        var present = new List<TodayEmployeeAttendanceDto>();
        var absent = new List<TodayEmployeeAttendanceDto>();
        var pendingReview = 0;
        foreach (var employee in employees)
        {
            var fullName = $"{employee.FirstName} {employee.LastName}".Trim();
            var code = !string.IsNullOrWhiteSpace(employee.EmployeeCode) ? employee.EmployeeCode : employee.EmpId.ToString();
            if (punchGroups.TryGetValue(employee.Id, out var rows) && rows.Count > 0)
            {
                var first = rows.First();
                var last = rows.Last();
                var verification = last.VerificationStatus ?? string.Empty;
                var isPending = !string.Equals(verification, "Success", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(verification, "ManualApproved", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(verification, "Approved", StringComparison.OrdinalIgnoreCase);
                if (isPending)
                {
                    pendingReview++;
                }

                present.Add(new TodayEmployeeAttendanceDto(
                    employee.Id,
                    code,
                    fullName,
                    employee.Department ?? string.Empty,
                    employee.Designation ?? string.Empty,
                    isPending ? "Needs Review" : "Present",
                    first.LocalPunchTime,
                    last.LocalPunchTime,
                    last.PunchType,
                    last.Source ?? string.Empty));
            }
            else
            {
                absent.Add(new TodayEmployeeAttendanceDto(
                    employee.Id,
                    code,
                    fullName,
                    employee.Department ?? string.Empty,
                    employee.Designation ?? string.Empty,
                    "Absent",
                    null,
                    null,
                    string.Empty,
                    string.Empty));
            }
        }

        var salesRows = await todaySalesQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(6)
            .Select(item => new { item.Id, item.InvoiceNumber, item.CustomerName, item.CustomerMobileNumber, item.BillAmount, item.OnDate, item.InvoiceStatus })
            .ToListAsync(cancellationToken);
        var purchaseRows = await todayPurchaseQuery
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(6)
            .Select(item => new { item.Id, item.InvoiceNumber, item.VendorName, item.BillAmount, item.OnDate, item.InvoiceStatus })
            .ToListAsync(cancellationToken);
        var voucherRows = await todayVouchers
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(6)
            .Select(item => new { item.Id, item.VoucherNumber, item.PartyName, item.Particulars, item.Amount, item.OnDate, item.VoucherType })
            .ToListAsync(cancellationToken);
        var cashVoucherRows = await todayCashVouchers
            .OrderByDescending(item => item.OnDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(6)
            .Select(item => new { item.Id, item.VoucherNumber, item.PartyName, item.Particulars, item.Amount, item.OnDate, item.VoucherType })
            .ToListAsync(cancellationToken);

        var recentActivities = salesRows.Select(item => new DashboardActivityDto(
                item.InvoiceNumber,
                item.CustomerName ?? item.CustomerMobileNumber ?? "Walk-in customer",
                FormatMoney(item.BillAmount),
                item.OnDate,
                item.InvoiceStatus.ToString(),
                "billing",
                item.Id))
            .Concat(purchaseRows.Select(item => new DashboardActivityDto(
                item.InvoiceNumber,
                item.VendorName ?? "Purchase inward",
                FormatMoney(item.BillAmount),
                item.OnDate,
                item.InvoiceStatus.ToString(),
                "purchase",
                item.Id)))
            .Concat(voucherRows.Select(item => new DashboardActivityDto(
                item.VoucherNumber,
                item.PartyName ?? item.Particulars ?? "Voucher",
                FormatMoney(item.Amount),
                item.OnDate,
                item.VoucherType.ToString(),
                "vouchers",
                item.Id)))
            .Concat(cashVoucherRows.Select(item => new DashboardActivityDto(
                item.VoucherNumber,
                item.PartyName ?? item.Particulars ?? "Cash voucher",
                FormatMoney(item.Amount),
                item.OnDate,
                item.VoucherType.ToString(),
                "cash-vouchers",
                item.Id)))
            .OrderByDescending(item => item.OnDate)
            .Take(12)
            .ToList();

        var trend = await TrendAsync(sales, purchases, nonGstDocuments, trendStart, tomorrow, cancellationToken);
        var scope = await ResolveScopeAsync(context, db, companyId, storeGroupId, storeId, "Today", cancellationToken);

        var metrics = new List<DashboardMetricDto>
        {
            Metric("Today's Sales", salesAmount, $"{invoiceCount} invoice(s)", "i-lucide-receipt-indian-rupee", "success"),
            Metric("Today's Purchase", purchaseAmount, $"{purchaseCount} inward bill(s)", "i-lucide-package-plus", "warning"),
            Metric("Receipts", totalReceipts, "Sales collections, voucher receipts and cash voucher receipts.", "i-lucide-arrow-down-left", "success"),
            Metric("Payments", totalPayments, "Purchase payments, voucher payments and cash voucher payments.", "i-lucide-arrow-up-right", "warning"),
            Metric("Expenses", totalExpenses, "Expense vouchers and cash expense vouchers.", "i-lucide-wallet-cards", "error"),
            Metric("Cash Vouchers", cashVoucherReceiptAmount + cashVoucherPaymentAmount + cashVoucherExpenseAmount, $"{cashVoucherCount} cash voucher row(s)", "i-lucide-banknote", "primary"),
            Metric("Present Employees", present.Count, "Active employees with at least one punch today.", "i-lucide-user-check", "success"),
            Metric("Absent Employees", absent.Count, "Active employees without a punch today.", "i-lucide-user-x", absent.Count > 0 ? "warning" : "success")
        };

        var quickActions = new List<DashboardQuickActionDto>
        {
            new("New Sale", "Open billing", "/billing", "i-lucide-receipt-indian-rupee", "primary", false),
            new("Store Operations", "Open/close store day", "/store-day", "i-lucide-sun-medium", "warning", false),
            new("Cash Voucher", "Record off-book cash movement", "/cash-vouchers", "i-lucide-wallet-cards", "neutral", false),
            new("Attendance Kiosk", "Mark employee attendance", "/attendance/kiosk", "i-lucide-camera", "success", absent.Count > 0),
            new("Purchase", "Open purchase inward", "/purchase", "i-lucide-package-plus", "neutral", false),
            new("Reports", "Open reports center", "/reports", "i-lucide-file-text", "neutral", false)
        };

        return new TodayDashboardDto(
            scope,
            businessDate,
            metrics,
            trend,
            new TodayCashFlowDto(
                salesCollectionAmount,
                purchasePaymentAmount,
                voucherReceiptAmount,
                voucherPaymentAmount,
                voucherExpenseAmount,
                cashVoucherReceiptAmount,
                cashVoucherPaymentAmount,
                cashVoucherExpenseAmount,
                totalReceipts,
                totalPayments,
                totalExpenses,
                netCashFlow),
            new TodayAttendanceSummaryDto(employees.Count, present.Count, absent.Count, pendingReview, present, absent),
            recentActivities,
            quickActions);
    }

    private static async Task<StoreManagerDashboardDto> StoreManagerAsync(
        HttpContext context,
        GarmetixDbContext db,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? storeGroupId,
        [FromQuery] Guid? storeId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var period = ResolvePeriod(from, to);

        var sales = FilterSales(WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context), companyId, storeId)
            .Where(item => !item.ReturnInvoice);
        var purchases = FilterPurchases(WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => !item.ReturnInvoice);
        var stocks = FilterStocks(WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking().Include(item => item.Product), context), companyId, storeGroupId, storeId);
        var onBookStocks = stocks.Where(item => !item.IsOFB);
        var nonGstDocuments = FilterNonGst(WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context), companyId, storeGroupId, storeId);

        var salesToday = await SumAsync(sales.Where(item => item.OnDate >= today && item.OnDate < tomorrow).Select(item => item.BillAmount), cancellationToken);
        var salesMonth = await SumAsync(sales.Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive).Select(item => item.BillAmount), cancellationToken);
        var purchaseMonth = await SumAsync(purchases.Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive).Select(item => item.BillAmount), cancellationToken);
        var stockValue = await SumAsync(onBookStocks.Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
        var onBookStockValue = stockValue;
        var nonGstStockValue = await SumAsync(stocks.Where(item => item.IsOFB).Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
        var lowStockCount = await onBookStocks.CountAsync(item => (item.PurchaseQty - item.SoldQty) <= 3, cancellationToken);
        var todayInvoiceCount = await sales.CountAsync(item => item.OnDate >= today && item.OnDate < tomorrow, cancellationToken);
        var dueInvoiceCount = await sales.CountAsync(item => item.BillAmount > item.PaidAmount, cancellationToken);
        var activeStockCount = await onBookStocks.CountAsync(item => (item.PurchaseQty - item.SoldQty) > 0, cancellationToken);
        var nonGstSalesMonth = await SumAsync(nonGstDocuments
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Sale && item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
            .Select(item => item.NetAmount), cancellationToken);
        var nonGstPurchaseMonth = await SumAsync(nonGstDocuments
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Purchase && item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
            .Select(item => item.NetAmount), cancellationToken);
        var gstMargin = salesMonth - purchaseMonth;
        var nonGstMargin = nonGstSalesMonth - nonGstPurchaseMonth;

        var trend = await TrendAsync(sales, purchases, nonGstDocuments, period.FromDate, period.ToExclusive, cancellationToken);
        var scope = await ResolveScopeAsync(context, db, companyId, storeGroupId, storeId, "Current store", cancellationToken);

        var recentSales = await sales
            .Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
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

        var stockAlerts = await onBookStocks
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
            new("Non-GST period", FormatMoney(nonGstSalesMonth - nonGstPurchaseMonth), "Separate register", "Non-GST sale minus purchase, excluded from GST return reports.", "i-lucide-file-warning", "primary")
        };

        return new StoreManagerDashboardDto(
            scope,
            [
                Metric("Today Sales", salesToday, "Current store collection", "i-lucide-receipt-indian-rupee", "success"),
                Metric("Period Sales", salesMonth, period.Dto.Label, "i-lucide-trending-up", "primary"),
                Metric("Period Purchase", purchaseMonth, period.Dto.Label, "i-lucide-package-plus", "warning"),
                Metric("Stock Value", stockValue, "On-book stock cost", "i-lucide-boxes", "neutral"),
                Metric("Invoices Today", todayInvoiceCount, "Bills created today", "i-lucide-file-check-2", "primary"),
                Metric("Low Stock", lowStockCount, "Qty at or below 3", "i-lucide-triangle-alert", lowStockCount > 0 ? "warning" : "success")
            ],
            trend,
            recentSales,
            stockAlerts,
            workQueue,
            quickActions,
            healthSignals,
            [
                Breakdown("GST sales", salesMonth, "success", "i-lucide-receipt-indian-rupee", "Regular tax invoice sales"),
                Breakdown("Off Book sales", nonGstSalesMonth, "primary", "i-lucide-file-warning", "Independent Non-GST register; excluded from books")
            ],
            [
                Breakdown("On-book stock", onBookStockValue, "success", "i-lucide-boxes", "Regular inventory valuation"),
                Breakdown("Off Book stock", nonGstStockValue, "primary", "i-lucide-package-open", "Independent Non-GST stock valuation")
            ],
            [
                Breakdown("GST margin", gstMargin, gstMargin >= 0 ? "success" : "error", "i-lucide-chart-no-axes-combined", "Sales minus purchase"),
                Breakdown("Off Book result", nonGstMargin, nonGstMargin >= 0 ? "primary" : "error", "i-lucide-file-warning", "Independent Non-GST sale minus purchase")
            ],
            period.Dto);
    }

    private static async Task<BusinessDashboardDto> BusinessAsync(
        HttpContext context,
        GarmetixDbContext db,
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? storeGroupId,
        [FromQuery] Guid? storeId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var period = ResolvePeriod(from, to);

        var sales = FilterSales(WorkspaceScope.ApplyTo(db.SalesInvoices.AsNoTracking(), context), companyId, storeId)
            .Where(item => !item.ReturnInvoice);
        var purchases = FilterPurchases(WorkspaceScope.ApplyTo(db.PurchaseInvoices.AsNoTracking(), context), companyId, storeGroupId, storeId)
            .Where(item => !item.ReturnInvoice);
        var stocks = FilterStocks(WorkspaceScope.ApplyTo(db.Stocks.AsNoTracking(), context), companyId, storeGroupId, storeId);
        var onBookStocks = stocks.Where(item => !item.IsOFB);
        var nonGstDocuments = FilterNonGst(WorkspaceScope.ApplyTo(db.NonGstGoodsDocuments.AsNoTracking(), context), companyId, storeGroupId, storeId);
        var customers = FilterCompany(WorkspaceScope.ApplyTo(db.Customers.AsNoTracking(), context), companyId);
        var vendors = FilterCompany(WorkspaceScope.ApplyTo(db.Vendors.AsNoTracking(), context), companyId);

        var salesMonth = await SumAsync(sales.Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive).Select(item => item.BillAmount), cancellationToken);
        var purchaseMonth = await SumAsync(purchases.Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive).Select(item => item.BillAmount), cancellationToken);
        var stockValue = await SumAsync(onBookStocks.Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
        var onBookStockValue = stockValue;
        var nonGstStockValue = await SumAsync(stocks.Where(item => item.IsOFB).Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
        var grossMargin = salesMonth - purchaseMonth;
        var customerCount = await customers.CountAsync(cancellationToken);
        var vendorCount = await vendors.CountAsync(cancellationToken);
        var invoiceCount = await sales.Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive).CountAsync(cancellationToken);
        var dueInvoiceCount = await sales.CountAsync(item => item.BillAmount > item.PaidAmount, cancellationToken);
        var lowStockCount = await onBookStocks.CountAsync(item => (item.PurchaseQty - item.SoldQty) <= 3, cancellationToken);
        var nonGstSalesMonth = await SumAsync(nonGstDocuments
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Sale && item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
            .Select(item => item.NetAmount), cancellationToken);
        var nonGstPurchaseMonth = await SumAsync(nonGstDocuments
            .Where(item => item.DocumentType == NonGstGoodsDocumentType.Purchase && item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
            .Select(item => item.NetAmount), cancellationToken);
        var gstMargin = salesMonth - purchaseMonth;
        var nonGstMargin = nonGstSalesMonth - nonGstPurchaseMonth;

        var trend = await TrendAsync(sales, purchases, nonGstDocuments, period.FromDate, period.ToExclusive, cancellationToken);
        var scope = await ResolveScopeAsync(context, db, companyId, storeGroupId, storeId, "Company / store group", cancellationToken);
        var storeRows = await StorePerformanceAsync(context, db, companyId, storeGroupId, storeId, period.FromDate, period.ToExclusive, cancellationToken);
        var storeGroupRows = await StoreGroupPerformanceAsync(context, db, companyId, storeGroupId, storeId, period.FromDate, period.ToExclusive, cancellationToken);
        var dashboardStoreIds = await ResolveStoreIdsAsync(context, db, companyId, storeGroupId, storeId, cancellationToken);
        var customerDues = await CustomerDueDashboardAsync(sales, dashboardStoreIds, cancellationToken);
        var vendorDues = await VendorDueDashboardAsync(purchases, db, cancellationToken);
        var cashPaymentSummary = await CashPaymentSummaryAsync(context, db, companyId, storeGroupId, storeId, period.FromDate, period.ToExclusive, cancellationToken);
        var storeGroupComparison = await StoreGroupComparisonDashboardAsync(context, db, companyId, storeGroupId, storeId, period.FromDate, period.ToExclusive, cancellationToken);

        var recentSales = await sales
            .Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
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
            .Where(item => item.OnDate >= period.FromDate && item.OnDate < period.ToExclusive)
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
                Metric("Period Sales", salesMonth, period.Dto.Label, "i-lucide-trending-up", "success"),
                Metric("Period Purchase", purchaseMonth, period.Dto.Label, "i-lucide-package-plus", "warning"),
                Metric("Gross Margin", grossMargin, "Sales minus purchase", "i-lucide-chart-no-axes-combined", grossMargin >= 0 ? "primary" : "error"),
                Metric("Stock Value", stockValue, "On-book stock cost", "i-lucide-boxes", "neutral"),
                Metric("Invoices", invoiceCount, period.Dto.Label, "i-lucide-file-check-2", "primary"),
                Metric("Customer Due", customerDues.Sum(item => item.DueAmount), $"{customerDues.Sum(item => item.BillCount)} open bill(s)", "i-lucide-hand-coins", customerDues.Count > 0 ? "warning" : "success"),
                Metric("Vendor Due", vendorDues.Sum(item => item.DueAmount), $"{vendorDues.Sum(item => item.BillCount)} open bill(s)", "i-lucide-receipt-text", vendorDues.Count > 0 ? "warning" : "success"),
                Metric("Net Cash", cashPaymentSummary.NetCash, "Collections minus payments", "i-lucide-wallet", cashPaymentSummary.NetCash >= 0 ? "success" : "error")
            ],
            trend,
            storeRows,
            storeGroupRows,
            recentSales,
            recentPurchases,
            adminQueue,
            quickActions,
            healthSignals,
            [
                Breakdown("GST sales", salesMonth, "success", "i-lucide-receipt-indian-rupee", "Regular tax invoice sales"),
                Breakdown("Off Book sales", nonGstSalesMonth, "primary", "i-lucide-file-warning", "Independent Non-GST register; excluded from books")
            ],
            [
                Breakdown("On-book stock", onBookStockValue, "success", "i-lucide-boxes", "Regular inventory valuation"),
                Breakdown("Off Book stock", nonGstStockValue, "primary", "i-lucide-package-open", "Independent Non-GST stock valuation")
            ],
            [
                Breakdown("GST margin", grossMargin, grossMargin >= 0 ? "success" : "error", "i-lucide-chart-no-axes-combined", "Sales minus purchase"),
                Breakdown("Off Book result", nonGstMargin, nonGstMargin >= 0 ? "primary" : "error", "i-lucide-file-warning", "Independent Non-GST sale minus purchase")
            ],
            period.Dto,
            customerDues,
            vendorDues,
            cashPaymentSummary,
            storeGroupComparison);
    }


    private static async Task<IReadOnlyList<PartyDueDashboardRowDto>> CustomerDueDashboardAsync(
        IQueryable<Garmetix.Core.Models.Inventory.Invoice> sales,
        IReadOnlyList<Guid> storeIds,
        CancellationToken cancellationToken)
    {
        if (storeIds.Count > 0)
        {
            sales = sales.Where(item => storeIds.Contains(item.StoreId));
        }

        var rows = await sales
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.BillAmount > item.PaidAmount)
            .Select(item => new
            {
                item.CustomerId,
                PartyName = item.CustomerName ?? item.CustomerMobileNumber ?? "Walk-in customer",
                Contact = item.CustomerMobileNumber ?? string.Empty,
                item.InvoiceNumber,
                item.BillAmount,
                item.PaidAmount,
                item.OnDate
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => new { item.CustomerId, item.PartyName, item.Contact })
            .Select(group =>
            {
                var oldest = group.Min(item => item.OnDate);
                return new PartyDueDashboardRowDto(
                    "Customer",
                    group.Key.CustomerId,
                    group.Key.PartyName,
                    group.Key.Contact,
                    group.Count(),
                    group.Sum(item => item.BillAmount),
                    group.Sum(item => item.PaidAmount),
                    group.Sum(item => Math.Max(0, item.BillAmount - item.PaidAmount)),
                    oldest,
                    DueAgeBucket(oldest));
            })
            .Where(item => item.DueAmount > 0)
            .OrderByDescending(item => item.DueAmount)
            .ThenBy(item => item.PartyName)
            .Take(12)
            .ToList();
    }

    private static async Task<IReadOnlyList<PartyDueDashboardRowDto>> VendorDueDashboardAsync(
        IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> purchases,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var invoiceRows = await purchases
            .Where(item => !item.Deleted && !item.ReturnInvoice && item.BillAmount > 0)
            .Select(item => new
            {
                item.Id,
                item.VendorId,
                PartyName = item.VendorName ?? "Vendor",
                Contact = item.VendorGSTIN ?? string.Empty,
                item.InvoiceNumber,
                item.BillAmount,
                item.OnDate,
                item.DueDate
            })
            .ToListAsync(cancellationToken);

        if (invoiceRows.Count == 0)
        {
            return [];
        }

        var invoiceIds = invoiceRows.Select(item => item.Id).ToList();
        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.PurchaseInvoiceId) && !item.Deleted)
            .GroupBy(item => item.PurchaseInvoiceId)
            .Select(group => new { PurchaseInvoiceId = group.Key, PaidAmount = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.PurchaseInvoiceId, item => item.PaidAmount, cancellationToken);

        return invoiceRows
            .Select(item => new
            {
                item.VendorId,
                item.PartyName,
                item.Contact,
                item.BillAmount,
                PaidAmount = payments.TryGetValue(item.Id, out var paid) ? paid : 0m,
                DueDate = item.DueDate == default ? item.OnDate : item.DueDate
            })
            .Where(item => item.BillAmount > item.PaidAmount)
            .GroupBy(item => new { item.VendorId, item.PartyName, item.Contact })
            .Select(group =>
            {
                var oldest = group.Min(item => item.DueDate);
                return new PartyDueDashboardRowDto(
                    "Vendor",
                    group.Key.VendorId,
                    group.Key.PartyName,
                    group.Key.Contact,
                    group.Count(),
                    group.Sum(item => item.BillAmount),
                    group.Sum(item => item.PaidAmount),
                    group.Sum(item => Math.Max(0, item.BillAmount - item.PaidAmount)),
                    oldest,
                    DueAgeBucket(oldest));
            })
            .OrderByDescending(item => item.DueAmount)
            .ThenBy(item => item.PartyName)
            .Take(12)
            .ToList();
    }

    private static async Task<CashPaymentSummaryDto> CashPaymentSummaryAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var storeIds = await ResolveStoreIdsAsync(context, db, companyId, storeGroupId, storeId, cancellationToken);
        var salesPayments = db.InvoicePayments.AsNoTracking()
            .Where(item => !item.Deleted && item.OnDate >= periodStart && item.OnDate < periodEndExclusive);
        var purchasePayments = db.PurchasePayments.AsNoTracking()
            .Where(item => !item.Deleted && item.OnDate >= periodStart && item.OnDate < periodEndExclusive);
        var vouchers = db.Vouchers.AsNoTracking()
            .Where(item => !item.Deleted && item.OnDate >= periodStart && item.OnDate < periodEndExclusive);

        if (companyId.HasValue)
        {
            salesPayments = salesPayments.Where(item => item.CompanyId == companyId.Value);
            purchasePayments = purchasePayments.Where(item => item.CompanyId == companyId.Value);
            vouchers = vouchers.Where(item => item.CompanyId == companyId.Value);
        }

        if (storeIds.Count > 0)
        {
            salesPayments = salesPayments.Where(item => storeIds.Contains(item.StoreId));
            purchasePayments = purchasePayments.Where(item => storeIds.Contains(item.StoreId));
            vouchers = vouchers.Where(item => storeIds.Contains(item.StoreId));
        }

        var salesRows = await salesPayments
            .GroupBy(item => item.PaymentMode)
            .Select(group => new { PaymentMode = group.Key, Amount = group.Sum(item => item.Amount), Count = group.Count() })
            .ToListAsync(cancellationToken);
        var purchaseRows = await purchasePayments
            .GroupBy(item => item.PaymentMode)
            .Select(group => new { PaymentMode = group.Key, Amount = group.Sum(item => item.Amount), Count = group.Count() })
            .ToListAsync(cancellationToken);
        var voucherRows = await vouchers
            .GroupBy(item => new { item.PaymentMode, item.VoucherType })
            .Select(group => new { group.Key.PaymentMode, group.Key.VoucherType, Amount = group.Sum(item => item.Amount), Count = group.Count() })
            .ToListAsync(cancellationToken);

        var modes = salesRows.Select(item => item.PaymentMode)
            .Concat(purchaseRows.Select(item => item.PaymentMode))
            .Concat(voucherRows.Select(item => item.PaymentMode))
            .Distinct()
            .OrderBy(item => item.ToString())
            .ToList();

        var paymentModes = modes.Select(mode =>
        {
            var salesAmount = salesRows.Where(item => item.PaymentMode == mode).Sum(item => item.Amount);
            var purchaseAmount = purchaseRows.Where(item => item.PaymentMode == mode).Sum(item => item.Amount);
            var voucherReceipt = voucherRows.Where(item => item.PaymentMode == mode && item.VoucherType == VoucherType.Receipt).Sum(item => item.Amount);
            var voucherPayment = voucherRows.Where(item => item.PaymentMode == mode && item.VoucherType != VoucherType.Receipt).Sum(item => item.Amount);
            var count = salesRows.Where(item => item.PaymentMode == mode).Sum(item => item.Count)
                + purchaseRows.Where(item => item.PaymentMode == mode).Sum(item => item.Count)
                + voucherRows.Where(item => item.PaymentMode == mode).Sum(item => item.Count);
            return new PaymentModeSummaryDto(mode.ToString(), salesAmount, purchaseAmount, voucherReceipt, voucherPayment, salesAmount + voucherReceipt - purchaseAmount - voucherPayment, count);
        }).ToList();

        var cashIn = paymentModes.Where(item => string.Equals(item.PaymentMode, PaymentMode.Cash.ToString(), StringComparison.OrdinalIgnoreCase)).Sum(item => item.SalesCollection + item.VoucherReceipt);
        var cashOut = paymentModes.Where(item => string.Equals(item.PaymentMode, PaymentMode.Cash.ToString(), StringComparison.OrdinalIgnoreCase)).Sum(item => item.PurchasePayment + item.VoucherPayment);
        var bankIn = paymentModes.Where(item => !string.Equals(item.PaymentMode, PaymentMode.Cash.ToString(), StringComparison.OrdinalIgnoreCase)).Sum(item => item.SalesCollection + item.VoucherReceipt);
        var bankOut = paymentModes.Where(item => !string.Equals(item.PaymentMode, PaymentMode.Cash.ToString(), StringComparison.OrdinalIgnoreCase)).Sum(item => item.PurchasePayment + item.VoucherPayment);

        return new CashPaymentSummaryDto(cashIn, cashOut, bankIn, bankOut, cashIn + bankIn - cashOut - bankOut, paymentModes);
    }

    private static async Task<IReadOnlyList<StoreGroupComparisonViewDto>> StoreGroupComparisonDashboardAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime periodStart,
        DateTime periodEndExclusive,
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

        var groupRows = await groups.OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);
        var result = new List<StoreGroupComparisonViewDto>();
        foreach (var group in groupRows)
        {
            var storeQuery = WorkspaceScope.ApplyTo(db.Stores.AsNoTracking(), context).Where(item => item.StoreGroupId == group.Id);
            if (storeId.HasValue)
            {
                storeQuery = storeQuery.Where(item => item.Id == storeId.Value);
            }

            var storeIds = await storeQuery.Select(item => item.Id).ToListAsync(cancellationToken);
            if (storeIds.Count == 0)
            {
                result.Add(new StoreGroupComparisonViewDto(group.Id, group.Name, 0, 0, 0, 0, 0, 0, 0, 0, 0));
                continue;
            }

            var sales = await SumAsync(db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && storeIds.Contains(item.StoreId) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.BillAmount), cancellationToken);
            var purchase = await SumAsync(db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId.HasValue && storeIds.Contains(item.StoreId.Value) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.BillAmount), cancellationToken);
            var customerDue = await SumAsync(db.SalesInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && storeIds.Contains(item.StoreId) && item.BillAmount > item.PaidAmount)
                .Select(item => item.BillAmount - item.PaidAmount), cancellationToken);
            var purchaseInvoices = await db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId.HasValue && storeIds.Contains(item.StoreId.Value))
                .Select(item => new { item.Id, item.BillAmount })
                .ToListAsync(cancellationToken);
            var purchaseInvoiceIds = purchaseInvoices.Select(item => item.Id).ToList();
            var purchasePaid = purchaseInvoiceIds.Count == 0
                ? 0
                : await SumAsync(db.PurchasePayments.AsNoTracking().Where(item => purchaseInvoiceIds.Contains(item.PurchaseInvoiceId) && !item.Deleted).Select(item => item.Amount), cancellationToken);
            var vendorDue = Math.Max(0, purchaseInvoices.Sum(item => item.BillAmount) - purchasePaid);
            var stockValue = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && !item.IsOFB && storeIds.Contains(item.StoreId))
                .Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
            var cashIn = await SumAsync(db.InvoicePayments.AsNoTracking()
                .Where(item => !item.Deleted && storeIds.Contains(item.StoreId) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.Amount), cancellationToken)
                + await SumAsync(db.Vouchers.AsNoTracking()
                .Where(item => !item.Deleted && storeIds.Contains(item.StoreId) && item.VoucherType == VoucherType.Receipt && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.Amount), cancellationToken);
            var cashOut = await SumAsync(db.PurchasePayments.AsNoTracking()
                .Where(item => !item.Deleted && storeIds.Contains(item.StoreId) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.Amount), cancellationToken)
                + await SumAsync(db.Vouchers.AsNoTracking()
                .Where(item => !item.Deleted && storeIds.Contains(item.StoreId) && item.VoucherType != VoucherType.Receipt && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.Amount), cancellationToken);

            result.Add(new StoreGroupComparisonViewDto(group.Id, group.Name, storeIds.Count, sales, purchase, customerDue, vendorDue, cashIn, cashOut, cashIn - cashOut, stockValue));
        }

        return result.OrderByDescending(item => item.Sales).ThenBy(item => item.StoreGroupName).ToList();
    }

    private static async Task<IReadOnlyList<Guid>> ResolveStoreIdsAsync(
        HttpContext context,
        GarmetixDbContext db,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
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

        return await stores.Select(item => item.Id).Distinct().ToListAsync(cancellationToken);
    }

    private static string DueAgeBucket(DateTime? oldestDate)
    {
        if (!oldestDate.HasValue)
        {
            return "No date";
        }

        var days = Math.Max(0, (DateTime.Today - oldestDate.Value.Date).Days);
        return days switch
        {
            <= 30 => "0-30 days",
            <= 60 => "31-60 days",
            <= 90 => "61-90 days",
            _ => "90+ days"
        };
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
        DateTime periodStart,
        DateTime periodEndExclusive,
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
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.BillAmount), cancellationToken);
            var purchases = await SumAsync(db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.BillAmount), cancellationToken);
            var stockValue = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && !item.IsOFB && item.StoreId == store.Id)
                .Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
            var currentStock = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && !item.IsOFB && item.StoreId == store.Id)
                .Select(item => item.PurchaseQty - item.SoldQty), cancellationToken);
            var invoiceCount = await db.SalesInvoices.AsNoTracking()
                .CountAsync(item => !item.Deleted && !item.ReturnInvoice && item.StoreId == store.Id && item.OnDate >= periodStart && item.OnDate < periodEndExclusive, cancellationToken);

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
        DateTime periodStart,
        DateTime periodEndExclusive,
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
                .Where(item => !item.Deleted && !item.ReturnInvoice && storeIds.Contains(item.StoreId) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.BillAmount), cancellationToken);
            var purchases = await SumAsync(db.PurchaseInvoices.AsNoTracking()
                .Where(item => !item.Deleted && !item.ReturnInvoice && item.StoreId.HasValue && storeIds.Contains(item.StoreId.Value) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive)
                .Select(item => item.BillAmount), cancellationToken);
            var stockValue = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && !item.IsOFB && storeIds.Contains(item.StoreId))
                .Select(item => (item.PurchaseQty - item.SoldQty) * item.CostPrice), cancellationToken);
            var currentStock = await SumAsync(db.Stocks.AsNoTracking()
                .Where(item => !item.Deleted && !item.IsOFB && storeIds.Contains(item.StoreId))
                .Select(item => item.PurchaseQty - item.SoldQty), cancellationToken);
            var invoiceCount = await db.SalesInvoices.AsNoTracking()
                .CountAsync(item => !item.Deleted && !item.ReturnInvoice && storeIds.Contains(item.StoreId) && item.OnDate >= periodStart && item.OnDate < periodEndExclusive, cancellationToken);

            result.Add(new StoreGroupPerformanceDto(group.Id, group.Name, storeIds.Count, sales, purchases, stockValue, invoiceCount, currentStock));
        }

        return result;
    }

    private static async Task<IReadOnlyList<DashboardTrendPointDto>> TrendAsync(
        IQueryable<Garmetix.Core.Models.Inventory.Invoice> sales,
        IQueryable<Garmetix.Core.Models.Inventory.PurchaseInvoice> purchases,
        IQueryable<Garmetix.Core.Models.Inventory.NonGstGoodsDocument> nonGstDocuments,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var totalDays = Math.Max(1, (periodEndExclusive.Date - periodStart.Date).Days);
        var chartDays = Math.Min(totalDays, 31);
        var rangeStart = periodEndExclusive.Date.AddDays(-chartDays);
        if (rangeStart < periodStart.Date)
        {
            rangeStart = periodStart.Date;
        }

        var salesRows = await sales
            .Where(item => item.OnDate >= rangeStart && item.OnDate < periodEndExclusive)
            .Select(item => new { item.OnDate, item.BillAmount })
            .ToListAsync(cancellationToken);
        var purchaseRows = await purchases
            .Where(item => item.OnDate >= rangeStart && item.OnDate < periodEndExclusive)
            .Select(item => new { item.OnDate, item.BillAmount })
            .ToListAsync(cancellationToken);
        var nonGstRows = await nonGstDocuments
            .Where(item => item.OnDate >= rangeStart && item.OnDate < periodEndExclusive)
            .Select(item => new { item.OnDate, item.DocumentType, item.NetAmount })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, Math.Max(1, (periodEndExclusive.Date - rangeStart.Date).Days))
            .Select(offset => rangeStart.Date.AddDays(offset))
            .Select(date =>
            {
                var daySales = salesRows.Where(item => item.OnDate.Date == date).Sum(item => item.BillAmount);
                var dayPurchase = purchaseRows.Where(item => item.OnDate.Date == date).Sum(item => item.BillAmount);
                var dayNonGstSales = nonGstRows.Where(item => item.OnDate.Date == date && item.DocumentType == NonGstGoodsDocumentType.Sale).Sum(item => item.NetAmount);
                var dayNonGstPurchase = nonGstRows.Where(item => item.OnDate.Date == date && item.DocumentType == NonGstGoodsDocumentType.Purchase).Sum(item => item.NetAmount);

                return new DashboardTrendPointDto(
                    date.ToString("dd MMM"),
                    date,
                    daySales,
                    dayPurchase,
                    daySales - dayPurchase,
                    dayNonGstSales,
                    dayNonGstPurchase);
            })
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

    private sealed record DashboardPeriod(DateTime FromDate, DateTime ToExclusive, DashboardPeriodDto Dto);

    private static DashboardPeriod ResolvePeriod(DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var defaultFrom = new DateTime(today.Year, today.Month, 1);
        var fromDate = (from?.Date ?? defaultFrom);
        var toDate = (to?.Date ?? today);

        if (toDate < fromDate)
        {
            toDate = fromDate;
        }

        if ((toDate - fromDate).TotalDays > 365)
        {
            fromDate = toDate.AddDays(-365);
        }

        var toExclusive = toDate.AddDays(1);
        var days = Math.Max(1, (toExclusive - fromDate).Days);
        var preset = ResolvePreset(today, fromDate, toDate);
        var label = fromDate == toDate
            ? fromDate.ToString("dd MMM yyyy")
            : $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";

        return new DashboardPeriod(fromDate, toExclusive, new DashboardPeriodDto(label, fromDate, toDate, days, preset));
    }

    private static string ResolvePreset(DateTime today, DateTime fromDate, DateTime toDate)
    {
        if (fromDate == today && toDate == today)
        {
            return "today";
        }
        if (fromDate == today.AddDays(-6) && toDate == today)
        {
            return "7d";
        }
        if (fromDate == today.AddDays(-29) && toDate == today)
        {
            return "30d";
        }
        if (fromDate == new DateTime(today.Year, today.Month, 1) && toDate == today)
        {
            return "month";
        }

        return "custom";
    }

    private static async Task<decimal> SumAsync(IQueryable<decimal> values, CancellationToken cancellationToken)
    {
        return await values.Select(item => (decimal?)item).SumAsync(cancellationToken) ?? 0m;
    }

    private static DashboardBreakdownDto Breakdown(string label, decimal value, string color, string icon, string caption)
    {
        return new DashboardBreakdownDto(label, value, FormatMetric(value), color, icon, caption);
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
