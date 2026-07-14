namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsTransactionBackfillReconciliationQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    string? Modules);

public sealed record FinalAccountsTransactionBackfillReconciliationResponse(
    DateTimeOffset GeneratedAtUtc,
    FinalAccountsScopeDto Scope,
    string StageName,
    bool WritesData,
    string BackupRequirement,
    IReadOnlyList<FinalAccountsTransactionBackfillSummaryDto> Summary,
    FinalAccountsBackfillPreviewResponse DryRunBackfill,
    FinalAccountsReconciliationResponse Reconciliation,
    FinalAccountsTransactionBackfillStatementEvidenceDto StatementEvidence,
    IReadOnlyList<FinalAccountsTransactionBackfillModuleEvidenceDto> ModuleEvidence,
    IReadOnlyList<FinalAccountsTransactionBackfillIssueDto> Issues,
    IReadOnlyList<FinalAccountsTransactionBackfillStepDto> ApprovalGates,
    IReadOnlyList<FinalAccountsTransactionBackfillStepDto> RollbackPlan);

public sealed record FinalAccountsTransactionBackfillSummaryDto(string Name, string Status, decimal Value, string Notes);

public sealed record FinalAccountsTransactionBackfillStatementEvidenceDto(
    DateTime? From,
    DateTime AsOf,
    string TrialBalanceStatus,
    decimal TrialBalanceDifference,
    string BalanceSheetStatus,
    decimal BalanceSheetDifference,
    decimal ProfitLossRevenue,
    decimal ProfitLossProfitAfterTax,
    int ProfitLossMappingIssueCount,
    int TrialBalanceDiagnosticCount,
    int BalanceSheetDiagnosticCount);

public sealed record FinalAccountsTransactionBackfillModuleEvidenceDto(
    string Module,
    int SourceCount,
    int PendingCount,
    int DriftCount,
    int PostedLinkCount,
    decimal SourceTotal,
    decimal JournalDebit,
    decimal JournalCredit,
    decimal Difference,
    int ExceptionCount,
    string Status,
    string SuggestedAction);

public sealed record FinalAccountsTransactionBackfillIssueDto(
    string Severity,
    string Code,
    int Count,
    string Message,
    string SuggestedAction);

public sealed record FinalAccountsTransactionBackfillStepDto(int StepNo, string Name, string Detail);
