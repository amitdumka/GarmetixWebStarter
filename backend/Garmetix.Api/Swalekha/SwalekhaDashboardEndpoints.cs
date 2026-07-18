using System.Text;
using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaBreakdownRow(string Category, decimal Value, string? Note);

public sealed record SwalekhaDashboardDto(
    decimal NetWorth,
    decimal TotalAssets,
    decimal TotalLiabilities,
    IReadOnlyList<SwalekhaBreakdownRow> AssetsBreakdown,
    IReadOnlyList<SwalekhaBreakdownRow> LiabilitiesBreakdown,
    IReadOnlyList<SwalekhaCalendarEventDto> UpcomingDues,
    IReadOnlyList<SwalekhaAppointmentDto> TodayAppointments);

/// <summary>
/// PersonalFin_14 - Dashboard &amp; Reports. NetWorth aggregates a live-computed current value
/// across every Swalekha entity built so far - bank/cash accounts, investments (FD/RD/Mutual
/// Funds/Shares/Other Assets), and the Person Ledger's receivables, minus Loans and payables.
/// FD/RD have no stored "current accrued value" field (only principal/installments-paid and an
/// optional maturity projection) - a deliberate, disclosed scope decision: FD counts at
/// PrincipalAmount and RD at MonthlyInstallment x InstallmentsPaid (amount invested so far, not a
/// fabricated interest-accrued figure). UpcomingDues reuses the same due-date computation
/// SwalekhaCalendarEndpoints established, generalized to a rolling 30-day window that can span a
/// calendar month boundary (the original endpoint's single-month projection can't).
/// </summary>
public static class SwalekhaDashboardEndpoints
{
    public static RouteGroupBuilder MapSwalekhaDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/dashboard")
            .WithTags("Swalekha Dashboard")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", GetDashboardAsync);
        group.MapGet("/export", ExportDashboardCsvAsync);

        return group;
    }

    private static async Task<IResult> GetDashboardAsync(SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(30);

        var (assets, liabilities) = await BuildNetWorthAsync(db, cancellationToken);
        var totalAssets = assets.Sum(a => a.Value);
        var totalLiabilities = liabilities.Sum(l => l.Value);

        var upcomingDues = await GetUpcomingDuesAsync(db, today, horizon, cancellationToken);

        var todayAppointments = await db.SwalekhaAppointments.AsNoTracking()
            .Where(a => a.StartAt >= today && a.StartAt < today.AddDays(1))
            .OrderBy(a => a.StartAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(new SwalekhaDashboardDto(
            totalAssets - totalLiabilities,
            totalAssets,
            totalLiabilities,
            assets,
            liabilities,
            upcomingDues,
            todayAppointments.Select(a => new SwalekhaAppointmentDto(a.Id, a.Title, a.Description, a.StartAt, a.EndAt, a.Location, a.IsAllDay, a.Notes, a.CreatedAt)).ToList()));
    }

    private static async Task<(List<SwalekhaBreakdownRow> Assets, List<SwalekhaBreakdownRow> Liabilities)> BuildNetWorthAsync(SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var assets = new List<SwalekhaBreakdownRow>();
        var liabilities = new List<SwalekhaBreakdownRow>();

        var accountsTotal = await db.SwalekhaAccounts.AsNoTracking().Where(a => a.IsActive).SumAsync(a => (decimal?)a.CurrentBalance, cancellationToken) ?? 0m;
        assets.Add(new SwalekhaBreakdownRow("Accounts (Bank/Cash/Credit Card)", accountsTotal, null));

        var fds = await db.SwalekhaFixedDeposits.AsNoTracking().Where(f => !f.IsClosed).ToListAsync(cancellationToken);
        assets.Add(new SwalekhaBreakdownRow("Fixed Deposits", fds.Sum(f => f.PrincipalAmount), "At invested principal, not accrued interest"));

        var rds = await db.SwalekhaRecurringDeposits.AsNoTracking().Where(r => !r.IsClosed).ToListAsync(cancellationToken);
        assets.Add(new SwalekhaBreakdownRow("Recurring Deposits", rds.Sum(r => r.MonthlyInstallment * r.InstallmentsPaid), "At amount invested so far, not accrued interest"));

        var funds = await db.SwalekhaMutualFunds.AsNoTracking().Where(f => f.IsActive).ToListAsync(cancellationToken);
        var mfValue = funds.Sum(f => f.CurrentNav.HasValue ? f.CurrentUnits * f.CurrentNav.Value : f.TotalInvested);
        assets.Add(new SwalekhaBreakdownRow("Mutual Funds", mfValue, null));

        var holdings = await db.SwalekhaShareHoldings.AsNoTracking().Where(h => h.IsActive).ToListAsync(cancellationToken);
        var sharesValue = holdings.Sum(h => h.CurrentPrice.HasValue ? h.CurrentQuantity * h.CurrentPrice.Value : h.TotalInvested);
        assets.Add(new SwalekhaBreakdownRow("Shares & Stocks", sharesValue, null));

        var otherAssetsTotal = await db.SwalekhaOtherAssets.AsNoTracking().Where(a => a.IsActive).SumAsync(a => (decimal?)a.CurrentValue, cancellationToken) ?? 0m;
        assets.Add(new SwalekhaBreakdownRow("Other Assets (PPF/EPF/NPS/Gold)", otherAssetsTotal, null));

        var immovableTotal = await db.SwalekhaAssets.AsNoTracking()
            .Where(a => a.IsActive && a.Category == SwalekhaAssetCategory.Immovable)
            .SumAsync(a => (decimal?)a.CurrentValue, cancellationToken) ?? 0m;
        if (immovableTotal != 0)
        {
            assets.Add(new SwalekhaBreakdownRow("Property (House/Flat/Land)", immovableTotal, null));
        }

        var movableTotal = await db.SwalekhaAssets.AsNoTracking()
            .Where(a => a.IsActive && a.Category == SwalekhaAssetCategory.Movable)
            .SumAsync(a => (decimal?)a.CurrentValue, cancellationToken) ?? 0m;
        if (movableTotal != 0)
        {
            assets.Add(new SwalekhaBreakdownRow("Movable Assets (Gold/Vehicle/Valuables)", movableTotal, null));
        }

        var contacts = await db.SwalekhaContacts.AsNoTracking().Where(c => c.IsActive).ToListAsync(cancellationToken);
        var receivables = contacts.Where(c => c.Balance > 0).Sum(c => c.Balance);
        var payables = contacts.Where(c => c.Balance < 0).Sum(c => Math.Abs(c.Balance));
        if (receivables != 0)
        {
            assets.Add(new SwalekhaBreakdownRow("Owed To You (Contacts)", receivables, null));
        }
        if (payables != 0)
        {
            liabilities.Add(new SwalekhaBreakdownRow("You Owe (Contacts)", payables, null));
        }

        var loansOutstanding = await db.SwalekhaLoans.AsNoTracking().Where(l => !l.IsClosed).SumAsync(l => (decimal?)l.OutstandingPrincipal, cancellationToken) ?? 0m;
        if (loansOutstanding != 0)
        {
            liabilities.Add(new SwalekhaBreakdownRow("Loans Outstanding", loansOutstanding, null));
        }

        return (assets, liabilities);
    }

    /// <summary>
    /// A rolling [from, to] window generalization of SwalekhaCalendarEndpoints.GetCalendarAsync's
    /// single-calendar-month due-date computation - recurring bills need their DueDayOfMonth
    /// projected into every month the window touches (up to two), not just one.
    /// </summary>
    private static async Task<List<SwalekhaCalendarEventDto>> GetUpcomingDuesAsync(SwalekhaDbContext db, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var events = new List<SwalekhaCalendarEventDto>();

        var bills = await db.SwalekhaRecurringBills.AsNoTracking().Where(b => b.IsActive).ToListAsync(cancellationToken);
        foreach (var bill in bills)
        {
            foreach (var monthStart in MonthsBetween(from, to))
            {
                var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
                var dueDay = Math.Min(bill.DueDayOfMonth, daysInMonth);
                var dueDate = new DateTime(monthStart.Year, monthStart.Month, dueDay);
                if (dueDate >= from && dueDate <= to)
                {
                    events.Add(new SwalekhaCalendarEventDto(dueDate, "RecurringBill", bill.Name, bill.Category, bill.Amount, bill.Id));
                }
            }
        }

        var fds = await db.SwalekhaFixedDeposits.AsNoTracking()
            .Where(f => !f.IsClosed && f.MaturityDate >= from && f.MaturityDate <= to)
            .ToListAsync(cancellationToken);
        events.AddRange(fds.Select(f => new SwalekhaCalendarEventDto(f.MaturityDate, "FixedDepositMaturity", $"FD Maturity - {f.BankName}", f.FdNumber, f.MaturityAmount, f.Id)));

        var rds = await db.SwalekhaRecurringDeposits.AsNoTracking()
            .Where(r => !r.IsClosed && r.MaturityDate >= from && r.MaturityDate <= to)
            .ToListAsync(cancellationToken);
        events.AddRange(rds.Select(r => new SwalekhaCalendarEventDto(r.MaturityDate, "RecurringDepositMaturity", $"RD Maturity - {r.BankName}", r.RdNumber, r.MaturityAmount, r.Id)));

        var policies = await db.SwalekhaInsurancePolicies.AsNoTracking().Where(p => p.IsActive && !p.IsMatured).ToListAsync(cancellationToken);
        foreach (var policy in policies)
        {
            var baseDate = policy.LastPremiumPaidDate ?? policy.StartDate;
            var nextDue = baseDate.AddMonths(FrequencyMonths(policy.PremiumFrequency));
            if (nextDue >= from && nextDue <= to)
            {
                events.Add(new SwalekhaCalendarEventDto(nextDue, "InsurancePremium", $"Premium Due - {policy.Insurer}", policy.PolicyType.ToString(), policy.PremiumAmount, policy.Id));
            }
        }

        var appointments = await db.SwalekhaAppointments.AsNoTracking()
            .Where(a => a.StartAt >= from && a.StartAt <= to)
            .ToListAsync(cancellationToken);
        events.AddRange(appointments.Select(a => new SwalekhaCalendarEventDto(a.StartAt, "Appointment", a.Title, a.Description, null, a.Id)));

        return events.OrderBy(e => e.Date).ToList();
    }

    private static IEnumerable<DateTime> MonthsBetween(DateTime from, DateTime to)
    {
        var cursor = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1);
        while (cursor <= end)
        {
            yield return cursor;
            cursor = cursor.AddMonths(1);
        }
    }

    private static int FrequencyMonths(SwalekhaPremiumFrequency frequency) => frequency switch
    {
        SwalekhaPremiumFrequency.Monthly => 1,
        SwalekhaPremiumFrequency.Quarterly => 3,
        SwalekhaPremiumFrequency.HalfYearly => 6,
        SwalekhaPremiumFrequency.Yearly => 12,
        _ => 12
    };

    private static async Task<IResult> ExportDashboardCsvAsync(SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var (assets, liabilities) = await BuildNetWorthAsync(db, cancellationToken);
        var totalAssets = assets.Sum(a => a.Value);
        var totalLiabilities = liabilities.Sum(l => l.Value);

        var sb = new StringBuilder();
        sb.AppendLine("Swalekha Net Worth & Investments Summary");
        sb.AppendLine($"Generated,{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("Section,Category,Value,Note");
        foreach (var row in assets)
        {
            sb.AppendLine($"Asset,{CsvEscape(row.Category)},{row.Value},{CsvEscape(row.Note ?? string.Empty)}");
        }
        foreach (var row in liabilities)
        {
            sb.AppendLine($"Liability,{CsvEscape(row.Category)},{row.Value},{CsvEscape(row.Note ?? string.Empty)}");
        }
        sb.AppendLine($"Total,Total Assets,{totalAssets},");
        sb.AppendLine($"Total,Total Liabilities,{totalLiabilities},");
        sb.AppendLine($"Total,Net Worth,{totalAssets - totalLiabilities},");
        sb.AppendLine();

        sb.AppendLine("Fixed Deposits");
        sb.AppendLine("Bank,FD Number,Principal,Interest Rate %,Maturity Date,Maturity Amount,Status");
        var fds = await db.SwalekhaFixedDeposits.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var fd in fds)
        {
            sb.AppendLine($"{CsvEscape(fd.BankName)},{CsvEscape(fd.FdNumber ?? string.Empty)},{fd.PrincipalAmount},{fd.InterestRatePercent},{fd.MaturityDate:yyyy-MM-dd},{fd.MaturityAmount},{(fd.IsClosed ? "Closed" : "Active")}");
        }
        sb.AppendLine();

        sb.AppendLine("Recurring Deposits");
        sb.AppendLine("Bank,RD Number,Monthly Installment,Installments Paid,Interest Rate %,Maturity Date,Status");
        var rds = await db.SwalekhaRecurringDeposits.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var rd in rds)
        {
            sb.AppendLine($"{CsvEscape(rd.BankName)},{CsvEscape(rd.RdNumber ?? string.Empty)},{rd.MonthlyInstallment},{rd.InstallmentsPaid},{rd.InterestRatePercent},{rd.MaturityDate:yyyy-MM-dd},{(rd.IsClosed ? "Closed" : "Active")}");
        }
        sb.AppendLine();

        sb.AppendLine("Mutual Funds");
        sb.AppendLine("Scheme,AMC,Units,Current NAV,Current Value,Total Invested");
        var funds = await db.SwalekhaMutualFunds.AsNoTracking().Where(f => f.IsActive).ToListAsync(cancellationToken);
        foreach (var fund in funds)
        {
            var value = fund.CurrentNav.HasValue ? fund.CurrentUnits * fund.CurrentNav.Value : fund.TotalInvested;
            sb.AppendLine($"{CsvEscape(fund.SchemeName)},{CsvEscape(fund.Amc ?? string.Empty)},{fund.CurrentUnits},{fund.CurrentNav},{value},{fund.TotalInvested}");
        }
        sb.AppendLine();

        sb.AppendLine("Shares & Stocks");
        sb.AppendLine("Symbol,Company,Quantity,Current Price,Current Value,Total Invested,Realized P&L");
        var holdings = await db.SwalekhaShareHoldings.AsNoTracking().Where(h => h.IsActive).ToListAsync(cancellationToken);
        foreach (var holding in holdings)
        {
            var value = holding.CurrentPrice.HasValue ? holding.CurrentQuantity * holding.CurrentPrice.Value : holding.TotalInvested;
            sb.AppendLine($"{CsvEscape(holding.Symbol)},{CsvEscape(holding.CompanyName ?? string.Empty)},{holding.CurrentQuantity},{holding.CurrentPrice},{value},{holding.TotalInvested},{holding.RealizedPnL}");
        }
        sb.AppendLine();

        sb.AppendLine("Insurance Policies (tax-relevant premiums)");
        sb.AppendLine("Insurer,Policy Type,Policy Number,Sum Assured,Premium Amount,Frequency,Next Due Date");
        var policies = await db.SwalekhaInsurancePolicies.AsNoTracking().Where(p => p.IsActive && !p.IsMatured).ToListAsync(cancellationToken);
        foreach (var policy in policies)
        {
            var baseDate = policy.LastPremiumPaidDate ?? policy.StartDate;
            var nextDue = baseDate.AddMonths(FrequencyMonths(policy.PremiumFrequency));
            sb.AppendLine($"{CsvEscape(policy.Insurer)},{policy.PolicyType},{CsvEscape(policy.PolicyNumber ?? string.Empty)},{policy.SumAssured},{policy.PremiumAmount},{policy.PremiumFrequency},{nextDue:yyyy-MM-dd}");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"swalekha-net-worth-{DateTime.UtcNow:yyyyMMdd}.csv";
        return Results.File(bytes, "text/csv", fileName);
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
