using Microsoft.Extensions.Options;

namespace Garmetix.Api.Backup;

public sealed class BackupAutomationHostedService(
    DatabaseBackupService backupService,
    IOptions<BackupOptions> options,
    ILogger<BackupAutomationHostedService> logger) : BackgroundService
{
    private readonly BackupOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Database backup automation is disabled.");
            return;
        }

        if (options.RunOnStartup)
        {
            await RunBackupAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = DelayUntilNextRun();
                logger.LogInformation("Next database backup is scheduled in {Delay}.", delay);
                await Task.Delay(delay, stoppingToken);
                await RunBackupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            var backup = await backupService.CreateBackupAsync("scheduled", cancellationToken);
            logger.LogInformation("Scheduled database backup completed: {FileName}.", backup.FileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled database backup failed.");
        }
    }

    private TimeSpan DelayUntilNextRun()
    {
        var now = LocalNow();
        var next = new DateTime(
            now.Year,
            now.Month,
            now.Day,
            Math.Clamp(options.RunHour, 0, 23),
            Math.Clamp(options.RunMinute, 0, 59),
            0);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    private DateTime LocalNow()
    {
        try
        {
            return TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId)).DateTime;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return DateTime.Now;
        }
    }
}
