namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsAccountingMasterAuditRules
{
    public const string StageName = "BS16AccountingMasterAudit";
    public const string AuditEndpointPath = "/api/final-accounts/audit/accounting-master";
    public const string BackupRequirement = "Run the deployed-host backup first before remote deploy or live audit: npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit";

    public static string NormalizeDuplicateKey(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    public static int PendingCount(int sourceRows, int linkedRows)
        => Math.Max(0, sourceRows - linkedRows);

    public static string SeverityForMissingLinks(int missingCount, int totalCount)
    {
        if (missingCount <= 0)
        {
            return "Info";
        }

        if (totalCount <= 0)
        {
            return "Info";
        }

        return missingCount == totalCount ? "Error" : "Warning";
    }

    public static bool IsReadOnlyAuditEndpoint(string path)
        => string.Equals(path, AuditEndpointPath, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<FinalAccountsAccountingRecommendationDto> Recommendations { get; } =
    [
        new("BS-16", "Do not mutate live accounting data", "Use this audit as discovery evidence only. Fixes and migrations belong to BS-17 and later after backup and human review."),
        new("BS-17", "Normalize ledger groups to Indian COA", "Align current ledger groups with Indian operational accounting practice and TallyPrime/BUSY/Marg-style primary groups."),
        new("BS-18", "Unify parties and ledgers", "Customer, Vendor, Employee and Other Party records should resolve to canonical party ledgers without duplicates."),
        new("BS-19", "Backfill only after reconciliation", "Run dry-run backfill and compare source totals, Trial Balance and Balance Sheet equality before any live mutation."),
        new("BS-20", "Make Final Accounts read canonical ledgers", "Mapping should become an exception tool after Books accounting masters are canonical.")
    ];
}
