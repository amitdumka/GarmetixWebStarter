using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsDirectLedgerIntegrationRules
{
    public const string StageName = "BS20FinalAccountsLedgerIntegration";
    public const string EvidenceEndpointPath = "/api/final-accounts/audit/direct-ledger-integration";
    public const string BackupRequirement = "Run the deployed-host backup before remote deploy or live direct-ledger review: npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS20FinalAccountsLedgerIntegration";

    public static IReadOnlyList<FinalAccountsDirectLedgerStepDto> IntegrationPlan { get; } =
    [
        new(1, "Take backup", "Run the BS20FinalAccountsLedgerIntegration deployed-host backup and confirm Backupfilehistory.md has a restore row."),
        new(2, "Capture direct ledger evidence", "Run this endpoint for the approved financial-year range and export the response."),
        new(3, "Review low-confidence ledger groups", "Resolve unmapped or low-confidence Books ledger-group classifications before switching statements."),
        new(4, "Compare statements", "Trial Balance, Profit & Loss and Balance Sheet values from canonical Books ledgers must tie out against Final Accounts evidence."),
        new(5, "Freeze exception mappings", "Keep Final Accounts mapping rows only for true import/exception gaps; do not require daily duplicate setup."),
        new(6, "Switch reports in a feature-gated batch", "Only after approval, make Final Accounts reports read the canonical Books ledger source by default."),
        new(7, "Smoke source postings", "Confirm Sales, Purchase, Voucher, Cash/Bank, Payroll, GST and Inventory source posting paths still create balanced Books journal lines.")
    ];

    public static IReadOnlyList<FinalAccountsDirectLedgerStepDto> RollbackPlan { get; } =
    [
        new(1, "Stop direct-ledger switch", "Keep the report-source flag on the existing Final Accounts journal source until evidence is approved."),
        new(2, "Restore from backup if needed", "Use the BS20 backup dump if a later direct-ledger migration damages canonical accounting data."),
        new(3, "Re-enable exception mapping", "If a source module cannot post canonical ledger lines, keep it on exception mapping until fixed."),
        new(4, "Re-run evidence", "Run this endpoint again after rollback and confirm Trial Balance and Balance Sheet status are balanced.")
    ];

    public static bool IsReadOnlyEvidenceEndpoint(string path)
        => string.Equals(path, EvidenceEndpointPath, StringComparison.OrdinalIgnoreCase);

    public static string StatusForDifference(decimal difference)
        => Math.Abs(FinalAccountsReportRules.RoundAmount(difference)) <= 0.01m ? "Balanced" : "Difference";

    public static string ClassificationStatus(int confidence, string ruleCode)
    {
        if (string.Equals(ruleCode, "UNMAPPED", StringComparison.OrdinalIgnoreCase) || confidence < 70)
        {
            return "ManualReview";
        }

        return confidence >= 90 ? "AutoSuggested" : "Review";
    }

    public static string SuggestedActionForClassification(int confidence, string ruleCode)
    {
        if (string.Equals(ruleCode, "UNMAPPED", StringComparison.OrdinalIgnoreCase) || confidence < 70)
        {
            return "Map this Books ledger group with Amit/CA before direct Final Accounts reporting.";
        }

        return confidence >= 90
            ? "Use as direct-ledger classification evidence; still require CA approval before switching reports."
            : "Review classification with CA before switching reports.";
    }

    public static decimal StatementAmount(decimal signedDebitMinusCredit, FinalAccountsAccountType accountType)
        => accountType is FinalAccountsAccountType.Asset or FinalAccountsAccountType.Expense or FinalAccountsAccountType.ContraLiability
            ? FinalAccountsReportRules.RoundAmount(signedDebitMinusCredit)
            : FinalAccountsReportRules.RoundAmount(-signedDebitMinusCredit);

    public static string BalanceType(decimal signedDebitMinusCredit)
        => signedDebitMinusCredit >= 0m ? "Debit" : "Credit";
}
