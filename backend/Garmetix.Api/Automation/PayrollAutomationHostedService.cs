using Garmetix.Api.Hr;
using Garmetix.Api.Payroll;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Automation;

public sealed class PayrollAutomationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<PayrollAutomationOptions> options,
    ILogger<PayrollAutomationHostedService> logger) : BackgroundService
{
    private readonly PayrollAutomationOptions options = options.Value;
    private DateOnly? lastAttendanceRunDate;
    private DateOnly? lastPayrollRunDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Payroll automation is disabled.");
            return;
        }

        if (options.RunOnStartup)
        {
            await RunIfDueAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun();
            logger.LogInformation("Next payroll automation check is scheduled in {Delay}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
                await RunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        var today = GetLocalToday();

        if (today.Day == DateTime.DaysInMonth(today.Year, today.Month) && lastAttendanceRunDate != today)
        {
            await GenerateMonthlyAttendanceAsync(today, cancellationToken);
            lastAttendanceRunDate = today;
        }

        if (today.Day == 1 && lastPayrollRunDate != today)
        {
            await GeneratePreviousMonthPayslipsAsync(today, cancellationToken);
            lastPayrollRunDate = today;
        }
    }

    private async Task GenerateMonthlyAttendanceAsync(DateOnly today, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MonthlyAttendanceService>();

        var response = await service.GenerateAsync(
            new GenerateMonthlyAttendanceRequest(today.Year, today.Month, null, null, null),
            cancellationToken);

        logger.LogInformation(
            "Monthly attendance automation completed for {Year}-{Month}. Employees: {Employees}, created: {Created}, updated: {Updated}.",
            response.Year,
            response.Month,
            response.EmployeesProcessed,
            response.RecordsCreated,
            response.RecordsUpdated);
    }

    private async Task GeneratePreviousMonthPayslipsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<PayrollService>();
        var previousMonth = today.AddMonths(-1);

        var response = await service.GeneratePayslipsAsync(
            new GeneratePayslipsRequest(previousMonth.Year, previousMonth.Month, null, null, null),
            cancellationToken);

        logger.LogInformation(
            "Payslip automation completed for {Year}-{Month}. Employees: {Employees}, created: {Created}, updated: {Updated}, due: {Due}.",
            response.Year,
            response.Month,
            response.EmployeesProcessed,
            response.PayslipsCreated,
            response.PayslipsUpdated,
            response.TotalDue);
    }

    private TimeSpan GetDelayUntilNextRun()
    {
        var now = GetLocalNow();
        var hour = Math.Clamp(options.RunHour, 0, 23);
        var minute = Math.Clamp(options.RunMinute, 0, 59);
        var nextRun = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);

        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }

    private DateOnly GetLocalToday()
    {
        return DateOnly.FromDateTime(GetLocalNow());
    }

    private DateTime GetLocalNow()
    {
        var timeZone = ResolveTimeZone();
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime;
    }

    private TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return ResolveFallbackTimeZone();
        }
        catch (InvalidTimeZoneException)
        {
            return ResolveFallbackTimeZone();
        }
    }

    private TimeZoneInfo ResolveFallbackTimeZone()
    {
        var fallbackId = options.TimeZoneId == "Asia/Kolkata" ? "India Standard Time" : "Asia/Kolkata";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(fallbackId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning("Time zone '{TimeZoneId}' was not available. Falling back to server local time.", options.TimeZoneId);
            return TimeZoneInfo.Local;
        }
    }
}
