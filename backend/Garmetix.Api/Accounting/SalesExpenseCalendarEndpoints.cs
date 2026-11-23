using Garmetix.Api.Auth;
using Garmetix.Core.Enums;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Accounting;

/// <summary>
/// One store, one month, day by day: sales (cash / non-cash / credit), expenses (expense vouchers, payment vouchers,
/// salary) and bank deposits, laid out for a calendar with a month summary. Uses the same definitions as Day Close
/// and Petty Cash so the figures agree with them.
/// </summary>
public static class SalesExpenseCalendarEndpoints
{
    public static RouteGroupBuilder MapSalesExpenseCalendarEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/accounting/sales-expense-calendar")
            .WithTags("Sales Expense Calendar")
            .RequireAuthorization(GarmetixPolicies.Accounting);
        group.MapGet("", GetCalendarAsync);
        return group;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed class DayTotals
    {
        public decimal Sale, Cash, NonCash, Credit, Returns, Expenses, Payments, Salary, BankDeposit;
        public int Bills;
    }

    private static async Task<IResult> GetCalendarAsync(
        Guid storeId,
        int? year,
        int? month,
        HttpContext context,
        GarmetixDbContext db,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (y < 2000 || y > 2100 || m is < 1 or > 12)
        {
            return Results.BadRequest(new { message = "Valid year and month are required." });
        }

        if (Guid.TryParse(context.User.FindFirst("storeId")?.Value, out var allowedStoreId) && allowedStoreId != storeId)
        {
            return Results.BadRequest(new { message = "The selected store is outside your access scope." });
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, cancellationToken);
        if (store is null)
        {
            return Results.BadRequest(new { message = "Select a valid store." });
        }

        var monthStart = new DateTime(y, m, 1);
        var nextMonth = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(y, m);
        var totals = Enumerable.Range(1, daysInMonth).ToDictionary(day => day, _ => new DayTotals());

        // Sales: every live (not cancelled) sale invoice of the month. Returns are counted apart, not netted.
        var invoices = await db.SalesInvoices.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= monthStart && item.OnDate < nextMonth && item.InvoiceStatus != InvoiceStatus.Cancelled)
            .Select(item => new { item.Id, item.OnDate, item.BillAmount, item.ReturnInvoice })
            .ToListAsync(cancellationToken);
        var saleIds = invoices.Where(item => !item.ReturnInvoice).Select(item => item.Id).ToList();
        var payments = saleIds.Count == 0
            ? []
            : await db.InvoicePayments.AsNoTracking()
                .Where(item => saleIds.Contains(item.InvoiceId))
                .Select(item => new { item.InvoiceId, item.PaymentMode, item.Amount })
                .ToListAsync(cancellationToken);
        var paymentsByInvoice = payments.ToLookup(item => item.InvoiceId);

        foreach (var invoice in invoices)
        {
            var bucket = totals[invoice.OnDate.Day];
            if (invoice.ReturnInvoice)
            {
                bucket.Returns += invoice.BillAmount;
                continue;
            }

            var cash = paymentsByInvoice[invoice.Id].Where(item => item.PaymentMode == PaymentMode.Cash).Sum(item => item.Amount);
            var nonCash = paymentsByInvoice[invoice.Id].Where(item => item.PaymentMode != PaymentMode.Cash).Sum(item => item.Amount);
            bucket.Bills++;
            bucket.Sale += invoice.BillAmount;
            bucket.Cash += cash;
            bucket.NonCash += nonCash;
            bucket.Credit += Math.Max(0, invoice.BillAmount - cash - nonCash);
        }

        // Expenses and payments: expense/payment vouchers (any payment mode) and cash vouchers, as Day Close reads them.
        var vouchers = await db.Vouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= monthStart && item.OnDate < nextMonth && item.VoucherType != VoucherType.Receipt)
            .Select(item => new { item.OnDate, item.VoucherType, item.Amount })
            .ToListAsync(cancellationToken);
        var cashVouchers = await db.CashVouchers.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= monthStart && item.OnDate < nextMonth && item.VoucherType != VoucherType.Receipt)
            .Select(item => new { item.OnDate, item.VoucherType, item.Amount })
            .ToListAsync(cancellationToken);
        foreach (var voucher in vouchers.Concat(cashVouchers))
        {
            var bucket = totals[voucher.OnDate.Day];
            if (voucher.VoucherType == VoucherType.Expense) bucket.Expenses += voucher.Amount;
            else bucket.Payments += voucher.Amount;
        }

        var salaries = await db.SalaryPayments.AsNoTracking()
            .Where(item => item.StoreId == storeId && !item.Deleted && item.OnDate >= monthStart && item.OnDate < nextMonth)
            .Select(item => new { item.OnDate, item.Amount })
            .ToListAsync(cancellationToken);
        foreach (var salary in salaries) totals[salary.OnDate.Day].Salary += salary.Amount;

        var deposits = await db.BankCashTranscations.AsNoTracking()
            .Where(item => item.StoreId == storeId && item.OnDate >= monthStart && item.OnDate < nextMonth && item.TransactionType == TransactionType.Deposit)
            .Select(item => new { item.OnDate, item.Amount })
            .ToListAsync(cancellationToken);
        foreach (var deposit in deposits) totals[deposit.OnDate.Day].BankDeposit += deposit.Amount;

        var days = totals.OrderBy(item => item.Key).Select(item =>
        {
            var d = item.Value;
            return new
            {
                date = new DateTime(y, m, item.Key).ToString("yyyy-MM-dd"),
                day = item.Key,
                bills = d.Bills,
                sale = Round(d.Sale),
                cashSale = Round(d.Cash),
                nonCashSale = Round(d.NonCash),
                creditSale = Round(d.Credit),
                returns = Round(d.Returns),
                expenses = Round(d.Expenses),
                payments = Round(d.Payments),
                salary = Round(d.Salary),
                totalExpense = Round(d.Expenses + d.Payments + d.Salary),
                bankDeposit = Round(d.BankDeposit)
            };
        }).ToList();

        // Averages run over the days elapsed so far in the current month (a half-finished month is not diluted by
        // days that have not happened yet); a past month uses all its days.
        var lastCounted = (y == today.Year && m == today.Month) ? today.Day : (new DateTime(y, m, 1) > today ? 0 : daysInMonth);
        var sale = days.Sum(item => item.sale);
        var totalExpense = days.Sum(item => item.totalExpense);

        return Results.Ok(new
        {
            store = new { store.Id, store.Name },
            year = y,
            month = m,
            firstDayOfWeek = (int)monthStart.DayOfWeek,
            days,
            summary = new
            {
                daysInMonth,
                daysCounted = lastCounted,
                billCount = days.Sum(item => item.bills),
                totalSale = Round(sale),
                cashSale = Round(days.Sum(item => item.cashSale)),
                nonCashSale = Round(days.Sum(item => item.nonCashSale)),
                creditSale = Round(days.Sum(item => item.creditSale)),
                salesReturns = Round(days.Sum(item => item.returns)),
                expenses = Round(days.Sum(item => item.expenses)),
                payments = Round(days.Sum(item => item.payments)),
                salary = Round(days.Sum(item => item.salary)),
                totalExpense = Round(totalExpense),
                averageSalePerDay = lastCounted == 0 ? 0m : Round(sale / lastCounted),
                averageExpensePerDay = lastCounted == 0 ? 0m : Round(totalExpense / lastCounted),
                bankDeposit = Round(days.Sum(item => item.bankDeposit)),
                daysWithSale = days.Count(item => item.sale > 0),
                bestDay = days.OrderByDescending(item => item.sale).Where(item => item.sale > 0).Select(item => new { item.date, item.sale }).FirstOrDefault()
            }
        });
    }
}
