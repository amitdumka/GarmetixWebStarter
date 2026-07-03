using System.Diagnostics;
using System.Text;
using Garmetix.Core.Models.Printing;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Garmetix.Api.DotMatrix;

public sealed class DotMatrixPrintWorker(IServiceScopeFactory scopeFactory, IOptions<DotMatrixPrintingOptions> options, ILogger<DotMatrixPrintWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunWorker)
        {
            logger.LogInformation("Dot-matrix print worker is disabled by configuration. Queue entries will be stored but not printed by the API container.");
            return;
        }

        logger.LogInformation("Dot-matrix print worker started in {OutputMode} mode.", options.Value.OutputMode);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dot-matrix print worker cycle failed.");
            }

            var delay = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 2, 60));
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarmetixDbContext>();
        var pending = await db.DotMatrixPrintQueueEntries
            .Where(item => item.Status == "Pending" || (item.Status == "Failed" && item.RetryCount < options.Value.RetryLimit))
            .OrderBy(item => item.BusinessDate)
            .ThenBy(item => item.SequenceNo)
            .Take(Math.Clamp(options.Value.BatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        foreach (var entry in pending)
        {
            try
            {
                var setting = await ResolveOutputSettingAsync(db, entry, cancellationToken);
                if (!setting.Enabled || setting.OutputMode == "Disabled")
                {
                    entry.Status = "Skipped";
                    entry.ErrorMessage = "Dot-matrix printing is disabled for this store.";
                    entry.UpdatedAt = NowUtc();
                    continue;
                }

                if (setting.OutputMode == "BridgeService")
                {
                    // Production-safe mode: the API container must not touch printer hardware or spool files.
                    // Leave the row Pending so the Ubuntu host-side DotMatrix Bridge can claim and print it.
                    continue;
                }

                if (!ShouldPrint(entry, setting))
                {
                    entry.Status = "Skipped";
                    entry.ErrorMessage = "This print event is disabled in dot-matrix settings.";
                    entry.UpdatedAt = NowUtc();
                    continue;
                }

                if (setting.OutputMode == "LpCommand")
                {
                    await PrintWithLpAsync(entry, setting, cancellationToken);
                }
                else
                {
                    await WriteSpoolFileAsync(entry, setting, cancellationToken);
                }

                entry.Status = "Printed";
                entry.PrintedAtUtc = NowUtc();
                entry.UpdatedAt = entry.PrintedAtUtc;
                entry.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                entry.Status = "Failed";
                entry.RetryCount += 1;
                entry.UpdatedAt = NowUtc();
                entry.ErrorMessage = ex.Message.Length > 480 ? ex.Message[..480] : ex.Message;
                logger.LogWarning(ex, "Dot-matrix print entry {EntryId} failed.", entry.Id);
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ResolvedOutputSetting> ResolveOutputSettingAsync(GarmetixDbContext db, DotMatrixPrintQueueEntry entry, CancellationToken cancellationToken)
    {
        var row = await db.DotMatrixPrintSettings.AsNoTracking().FirstOrDefaultAsync(item => item.StoreId == entry.StoreId && !item.Deleted, cancellationToken);
        var enabled = row?.Enabled ?? options.Value.Enabled;
        var mode = NormalizeOutputMode(row?.OutputMode ?? options.Value.OutputMode);
        var printer = FirstNonBlank(entry.PrinterName, row?.PrinterName, options.Value.PrinterName);
        var spoolDir = FirstNonBlank(row?.SpoolDirectory, options.Value.SpoolDirectory, "/app/data/dotmatrix-spool");
        return new ResolvedOutputSetting(
            enabled,
            mode,
            printer,
            spoolDir,
            row?.PrintTransactions ?? true,
            row?.PrintDayOpeningClosing ?? true,
            row?.PrintEditsAndDeletes ?? true);
    }

    private async Task WriteSpoolFileAsync(DotMatrixPrintQueueEntry entry, ResolvedOutputSetting setting, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(setting.SpoolDirectory);
        var fileName = $"{entry.BusinessDate:yyyyMMdd}-{entry.SequenceNo}-{entry.Id:N}.txt";
        var path = Path.Combine(setting.SpoolDirectory, fileName);
        await File.WriteAllTextAsync(path, NormalizeText(entry.PrintableText), Encoding.UTF8, cancellationToken);
    }

    private async Task PrintWithLpAsync(DotMatrixPrintQueueEntry entry, ResolvedOutputSetting setting, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(setting.PrinterName))
        {
            throw new InvalidOperationException("PrinterName is required for LpCommand mode.");
        }

        var args = options.Value.LpArgumentsTemplate.Replace("{printer}", QuoteArgument(setting.PrinterName), StringComparison.OrdinalIgnoreCase);
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(options.Value.LpCommand) ? "lp" : options.Value.LpCommand,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.Start();
        await process.StandardInput.WriteAsync(NormalizeText(entry.PrintableText));
        process.StandardInput.Close();
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"lp failed with exit code {process.ExitCode}: {error}");
        }
    }

    private static bool ShouldPrint(DotMatrixPrintQueueEntry entry, ResolvedOutputSetting setting)
    {
        if (entry.EventType == "Test") return true;
        if (entry.EventType == "Transaction") return setting.PrintTransactions;
        if (entry.EventType == "AuditMutation") return setting.PrintEditsAndDeletes;
        if (entry.EventType is "DayOpening" or "DayClosing") return setting.PrintDayOpeningClosing;
        return true;
    }

    private static string NormalizeText(string value) => value.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    private static DateTime NowUtc() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    private static string FirstNonBlank(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    private static string NormalizeOutputMode(string? value) => string.Equals(value, "BridgeService", StringComparison.OrdinalIgnoreCase) ? "BridgeService" : string.Equals(value, "LpCommand", StringComparison.OrdinalIgnoreCase) ? "LpCommand" : string.Equals(value, "Disabled", StringComparison.OrdinalIgnoreCase) ? "Disabled" : string.Equals(value, "SpoolFile", StringComparison.OrdinalIgnoreCase) ? "SpoolFile" : "BridgeService";
    private static string QuoteArgument(string value) => value.Contains(' ') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    private sealed record ResolvedOutputSetting(bool Enabled, string OutputMode, string PrinterName, string SpoolDirectory, bool PrintTransactions, bool PrintDayOpeningClosing, bool PrintEditsAndDeletes);
}
