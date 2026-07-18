using Garmetix.Infrastructure.Data;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.Communication;

/// <summary>
/// Bounded periodic background worker that drains the EmailQueueItems outbox. Same shape as
/// OracleSecondarySyncHostedService (IOptionsMonitor for live-reloadable Enabled/interval,
/// PeriodicTimer, a RunSafelyAsync wrapper that swallows OperationCanceledException and logs
/// everything else so one bad tick never kills the worker).
/// </summary>
public sealed class EmailQueueWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<EmailQueueOptions> optionsMonitor,
    ILogger<EmailQueueWorker> logger) : BackgroundService
{
    private readonly string _workerOwner = $"{System.Net.Dns.GetHostName()}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var current = optionsMonitor.CurrentValue;
        if (!current.Enabled)
        {
            logger.LogInformation("EmailQueueWorker is disabled via configuration (Communication:EmailQueue:Enabled=false).");
            return;
        }

        if (current.RunOnStartup)
        {
            await RunSafelyAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, optionsMonitor.CurrentValue.PollIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!optionsMonitor.CurrentValue.Enabled)
            {
                continue;
            }

            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task RunSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var options = optionsMonitor.CurrentValue;
            var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
            var claimService = new EmailQueueClaimService(db);

            var recovered = await claimService.RecoverStaleLeasesAsync(stoppingToken);
            if (recovered > 0)
            {
                logger.LogWarning("EmailQueueWorker recovered {Count} stale processing lease(s).", recovered);
            }

            var claimed = await claimService.ClaimBatchAsync(options.BatchSize, TimeSpan.FromSeconds(options.LeaseDurationSeconds), _workerOwner, stoppingToken);
            if (claimed.Count == 0)
            {
                return;
            }

            var processor = new EmailQueueItemProcessor(
                db,
                scope.ServiceProvider.GetRequiredService<EmailProviderResolutionService>(),
                scope.ServiceProvider.GetRequiredService<IEmailProviderClientFactory>(),
                scope.ServiceProvider.GetRequiredService<EmailRateLimitService>(),
                scope.ServiceProvider.GetRequiredService<CommunicationAttachmentStorageService>(),
                options);

            foreach (var item in claimed)
            {
                try
                {
                    await processor.ProcessAsync(item, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "EmailQueueWorker failed to process queue item {QueueItemId}.", item.Id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "EmailQueueWorker tick failed.");
        }
    }
}
