namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsDirectLedgerIntegrationQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    DateTime? AsOf);

public sealed record FinalAccountsDirectLedgerIntegrationResponse(
    DateTimeOffset GeneratedAtUtc,
    FinalAccountsScopeDto Scope,
    string StageName,
    bool WritesData,
    string BackupRequirement,
    IReadOnlyList<FinalAccountsDirectLedgerSummaryDto> Summary,
    IReadOnlyList<FinalAccountsDirectLedgerTrialBalanceDto> CanonicalTrialBalance,
    IReadOnlyList<FinalAccountsDirectLedgerStatementComparisonDto> StatementComparisons,
    IReadOnlyList<FinalAccountsDirectLedgerGroupClassificationDto> GroupClassifications,
    IReadOnlyList<FinalAccountsDirectLedgerSourceTypeDto> SourceTypes,
    IReadOnlyList<FinalAccountsDirectLedgerIssueDto> Issues,
    IReadOnlyList<FinalAccountsDirectLedgerStepDto> IntegrationPlan,
    IReadOnlyList<FinalAccountsDirectLedgerStepDto> RollbackPlan);

public sealed record FinalAccountsDirectLedgerSummaryDto(string Name, decimal Value, string Notes);

public sealed record FinalAccountsDirectLedgerTrialBalanceDto(
    Guid LedgerId,
    string LedgerName,
    Guid LedgerGroupId,
    string LedgerGroupName,
    string AccountType,
    string NaturalBalance,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit,
    string BalanceType,
    string StatementCategory,
    string ClassificationRule);

public sealed record FinalAccountsDirectLedgerStatementComparisonDto(
    string Statement,
    string Metric,
    decimal BooksLedgerValue,
    decimal FinalAccountsValue,
    decimal Difference,
    string Status,
    string Notes);

public sealed record FinalAccountsDirectLedgerGroupClassificationDto(
    Guid LedgerGroupId,
    string LedgerGroupName,
    string ExistingCategory,
    int LedgerCount,
    string RecommendedPrimaryGroup,
    string AccountType,
    string NaturalBalance,
    int Confidence,
    string RuleCode,
    string Status,
    string SuggestedAction);

public sealed record FinalAccountsDirectLedgerSourceTypeDto(
    string SourceType,
    int JournalEntryCount,
    int JournalLineCount,
    decimal Debit,
    decimal Credit,
    decimal Difference,
    string Status);

public sealed record FinalAccountsDirectLedgerIssueDto(
    string Severity,
    string Code,
    int Count,
    string Message,
    string SuggestedAction);

public sealed record FinalAccountsDirectLedgerStepDto(int StepNo, string Name, string Detail);
