using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsSyncRules
{
    public static readonly IReadOnlyList<string> DefaultModules = ["Sales", "Purchase", "CashBank", "Gst", "Inventory", "Payroll"];
    private static readonly string[] OpeningOrMigrationInventorySourceTypes = ["RegularUnbilledOpeningStockImport"];

    public static IReadOnlyList<string> NormalizeModules(IReadOnlyList<string>? modules)
    {
        if (modules is null || modules.Count == 0)
        {
            return DefaultModules;
        }

        var normalized = modules
            .Select(item => (item ?? string.Empty).Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(NormalizeModule)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return normalized.Count == 0 ? DefaultModules : normalized;
    }

    public static string NormalizeModule(string module)
    {
        var token = (module ?? string.Empty).Trim().Replace("-", string.Empty).Replace("_", string.Empty);
        return token.ToUpperInvariant() switch
        {
            "SALE" or "SALES" => "Sales",
            "PURCHASE" or "PURCHASES" => "Purchase",
            "CASH" or "BANK" or "CASHBANK" or "RECEIPT" or "PAYMENT" => "CashBank",
            "GST" or "TAX" or "TDS" => "Gst",
            "INVENTORY" or "STOCK" or "COGS" => "Inventory",
            "PAYROLL" or "SALARY" => "Payroll",
            _ => throw new ArgumentException($"Backfill module '{module}' is not supported.")
        };
    }

    public static string NormalizeIdempotencyKey(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    public static string BuildResumeCheckpoint(IReadOnlyList<string> modules, DateTime? from, DateTime? to)
        => string.Join("|", [
            string.Join(",", modules.Select(NormalizeModule).OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
            from?.ToString("O") ?? "beginning",
            to?.ToString("O") ?? "open"
        ]);

    public static string BuildJobNumber(DateTime nowUtc, string? idempotencyKey)
    {
        var key = NormalizeIdempotencyKey(idempotencyKey);
        return string.IsNullOrWhiteSpace(key)
            ? $"FA-SYNC-{nowUtc:yyyyMMddHHmmss}"
            : $"FA-SYNC-{Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(key)):X8}";
    }

    public static FinalAccountsSyncItemStatus StatusForSource(bool alreadyLinked, bool drifted)
    {
        if (drifted)
        {
            return FinalAccountsSyncItemStatus.Drifted;
        }

        return alreadyLinked ? FinalAccountsSyncItemStatus.SkippedExisting : FinalAccountsSyncItemStatus.Pending;
    }

    public static bool ShouldExcludeInventoryBackfillCandidate(IReadOnlyList<string> sourceTypes, decimal sourceAmount)
    {
        if (Math.Abs(FinalAccountsJournalRules.RoundAmount(sourceAmount)) <= 0.01m)
        {
            return true;
        }

        return sourceTypes.Any(sourceType => OpeningOrMigrationInventorySourceTypes.Contains(sourceType, StringComparer.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> InventoryAdapterKeys(IReadOnlyList<string> sourceTypes)
    {
        if (sourceTypes.Any(item => item is "SalesInvoice" or "SalesExchange" or "VyaparSaleImport"))
        {
            return ["sale-cogs"];
        }

        if (sourceTypes.Any(item => item is "SalesReturn" or "SalesInvoiceCancellation"))
        {
            return ["sale-return-stock-restoration"];
        }

        if (sourceTypes.Any(item => item is "PurchaseInvoice" or "PurchaseInvoiceImport"))
        {
            return ["purchase-inventory"];
        }

        if (sourceTypes.Any(item => item == "PurchaseReturn"))
        {
            return ["purchase-return-inventory"];
        }

        if (sourceTypes.Any(item => item == "StockOperationDocument"))
        {
            return ["stock-adjustment", "stock-transfer"];
        }

        return [];
    }

    public static bool ShouldContinueAfterFailure(bool stopOnError, int failedCount)
        => !stopOnError || failedCount == 0;

    public static DateTime NextRetryAt(DateTime nowUtc, int attemptCount, int retryDelaySeconds)
    {
        var boundedAttempt = Math.Clamp(attemptCount, 1, 10);
        var boundedDelay = Math.Clamp(retryDelaySeconds, 5, 3600);
        return nowUtc.AddSeconds(boundedDelay * boundedAttempt);
    }
}
