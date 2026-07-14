namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsQaRules
{
    public const long LargeExportWarningBytes = 25 * 1024 * 1024;

    public static IReadOnlyList<string> RequiredBrowserRoutes { get; } =
    [
        "/final-accounts",
        "/final-accounts/setup",
        "/final-accounts/chart-of-accounts",
        "/final-accounts/fiscal-periods",
        "/final-accounts/general-ledger",
        "/final-accounts/posting-rules",
        "/final-accounts/reports",
        "/final-accounts/ca-workspace",
        "/final-accounts/closeout",
        "/final-accounts/projections",
        "/final-accounts/exchange"
    ];

    public static IReadOnlyList<string> RequiredKeyboardRoutes { get; } =
    [
        "/final-accounts",
        "/final-accounts/general-ledger",
        "/final-accounts/reports",
        "/final-accounts/ca-workspace",
        "/final-accounts/closeout",
        "/final-accounts/exchange"
    ];

    public static IReadOnlyList<string> ForbiddenMigrationTokens { get; } =
    [
        "DropTable(",
        "DropColumn(",
        "RenameTable(",
        "RenameColumn("
    ];

    public static bool IsAdditiveMigrationLine(string line)
        => !ForbiddenMigrationTokens.Any(token => line.Contains(token, StringComparison.OrdinalIgnoreCase));

    public static bool IsLargeExport(long byteLength)
        => byteLength > LargeExportWarningBytes;

    public static bool IsProductionHostChange(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/deploy/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".env", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".env.production", StringComparison.OrdinalIgnoreCase);
    }

    public static string FeatureDisabledStatus()
        => "403:/final-accounts/setup";
}
