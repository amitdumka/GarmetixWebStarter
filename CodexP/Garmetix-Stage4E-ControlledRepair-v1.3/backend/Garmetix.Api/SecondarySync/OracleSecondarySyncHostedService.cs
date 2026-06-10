using Microsoft.Extensions.Options;

namespace Garmetix.Api.SecondarySync;

public sealed class OracleSecondarySyncHostedService(
    OracleSecondarySyncService syncService,
    IOptionsMonitor<OracleSecondarySyncOptions> options,
    ILogger<OracleSecondarySyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var current = options.CurrentValue;
        if (!current.Enabled)
        {
            logger.LogInformation("Oracle secondary sync is disabled.");
            return;
        }

        if (current.RunOnStartup)
        {
            await RunSafelyAsync(stoppingToken);
        }

        var intervalSeconds = Math.Max(60, options.CurrentValue.IntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!options.CurrentValue.Enabled)
            {
                continue;
            }

            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await syncService.RunOnceAsync(repairFirst: true, cancellationToken: cancellationToken);
            if (result.Success)
            {
                logger.LogInformation("Oracle secondary sync completed. {TotalPushed} event(s) pushed.", result.TotalPushed);
            }
            else
            {
                logger.LogWarning("Oracle secondary sync did not complete successfully: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Oracle secondary sync hosted run failed.");
        }
    }
}
