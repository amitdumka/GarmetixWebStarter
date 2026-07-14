namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsTransactionBackfillReconciliationRules
{
    public const string StageName = "BS19TransactionBackfillReconciliation";
    public const string EvidenceEndpointPath = "/api/final-accounts/audit/transaction-backfill-reconciliation";
    public const string BackupRequirement = "Run the deployed-host backup before remote deploy or live reconciliation review: npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS19TransactionBackfillReconciliation";

    public static IReadOnlyList<FinalAccountsTransactionBackfillStepDto> ApprovalGates { get; } =
    [
        new(1, "Backup exists", "Confirm BS19TransactionBackfillReconciliation backup and Backupfilehistory.md row exist on the deployed host."),
        new(2, "Dry-run captured", "Capture this endpoint output for the full selected period and module list."),
        new(3, "No drift", "SourceHashDrift rows must be resolved or explicitly approved before any posting/backfill."),
        new(4, "Source control reconciles", "Source totals, linked journal totals and exception counts must be reviewed by module."),
        new(5, "Trial Balance equals", "Trial Balance debit and credit totals must be equal after any approved dry-run/backfill batch."),
        new(6, "Balance Sheet equals", "Balance Sheet assets must equal liabilities plus capital/equity."),
        new(7, "P&L tie-out reviewed", "Profit & Loss revenue/profit and mapping issues must be reviewed by Amit/CA."),
        new(8, "Human approval", "Do not start live mutation without Amit/CA approval and a restore decision point.")
    ];

    public static IReadOnlyList<FinalAccountsTransactionBackfillStepDto> RollbackPlan { get; } =
    [
        new(1, "Stop backfill", "Disable backfill/sync job creation and stop any running batch."),
        new(2, "Use backup if needed", "Restore the BS19 backup dump if posted links/journals cannot be reversed safely."),
        new(3, "Reverse by source link", "For reversible batches, reverse Final Accounts journals using source posting links rather than editing posted rows."),
        new(4, "Re-run evidence", "Run this endpoint again and compare dry-run, Trial Balance, Balance Sheet and P&L evidence before reopening."),
        new(5, "Document exceptions", "Keep unresolved modules out of BS-20 direct integration until their exception report is cleared.")
    ];

    public static IReadOnlyList<string>? ParseModules(string? modules)
        => string.IsNullOrWhiteSpace(modules)
            ? null
            : modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public static string StatusForDifference(decimal difference, int exceptionCount, int driftCount)
    {
        if (Math.Abs(difference) > 0.01m)
        {
            return "Difference";
        }

        if (exceptionCount > 0 || driftCount > 0)
        {
            return "Review";
        }

        return "Balanced";
    }

    public static string SuggestedActionForModule(string status, int pendingCount)
        => status switch
        {
            "Balanced" when pendingCount == 0 => "No backfill action suggested for this module.",
            "Balanced" => "Pending rows remain; review dry-run preview before any approved backfill.",
            "Review" => "Resolve drift or exceptions before live posting.",
            _ => "Do not backfill this module until source/journal difference is explained."
        };

    public static bool IsReadOnlyEvidenceEndpoint(string path)
        => string.Equals(path, EvidenceEndpointPath, StringComparison.OrdinalIgnoreCase);
}
