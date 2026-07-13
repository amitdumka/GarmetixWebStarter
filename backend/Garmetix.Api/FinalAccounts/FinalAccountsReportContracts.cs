namespace Garmetix.Api.FinalAccounts;

public sealed record FinalAccountsGeneralLedgerReportQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid? AccountId,
    DateTime? From,
    DateTime? To,
    bool? IncludeReversed,
    int? Page,
    int? PageSize);

public sealed record FinalAccountsTrialBalanceReportQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    string? View,
    bool? IncludeZeroBalances,
    string? Comparison);

public sealed record FinalAccountsProfitLossReportQuery(
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    DateTime? From,
    DateTime? To,
    string? View,
    string? RoundingUnit,
    bool? HideZero);

public sealed record FinalAccountsGeneralLedgerReportResponse(
    int Page,
    int PageSize,
    int TotalCount,
    DateTime? From,
    DateTime? To,
    Guid? AccountId,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit,
    IReadOnlyList<FinalAccountsGeneralLedgerRowDto> Rows,
    IReadOnlyList<FinalAccountsValidationIssueDto> Issues);

public sealed record FinalAccountsGeneralLedgerRowDto(
    Guid JournalEntryId,
    Guid JournalLineId,
    string EntryNumber,
    DateTime OnDate,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string NaturalBalance,
    Guid? StoreId,
    string Status,
    string SourceType,
    Guid? SourceId,
    string? ReferenceNumber,
    string? Narration,
    decimal Debit,
    decimal Credit,
    decimal RunningDebit,
    decimal RunningCredit,
    decimal RunningBalance,
    string BalanceType,
    bool IsReversal,
    bool IsReversed,
    string JournalDrillDownPath,
    string SourceDrillDownPath);

public sealed record FinalAccountsTrialBalanceReportResponse(
    string View,
    string Comparison,
    DateTime? From,
    DateTime? To,
    bool IncludeZeroBalances,
    decimal TotalOpeningDebit,
    decimal TotalOpeningCredit,
    decimal TotalPeriodDebit,
    decimal TotalPeriodCredit,
    decimal TotalClosingDebit,
    decimal TotalClosingCredit,
    decimal Difference,
    string Status,
    IReadOnlyList<FinalAccountsTrialBalanceRowDto> Rows,
    IReadOnlyList<FinalAccountsTrialBalanceComparisonDto> Comparisons,
    IReadOnlyList<FinalAccountsValidationIssueDto> Diagnostics);

public sealed record FinalAccountsTrialBalanceRowDto(
    Guid? GroupId,
    string GroupCode,
    string GroupName,
    Guid? AccountId,
    string AccountCode,
    string AccountName,
    string AccountType,
    string NaturalBalance,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingDebit,
    decimal ClosingCredit,
    string BalanceType,
    string DrillDownPath);

public sealed record FinalAccountsTrialBalanceComparisonDto(
    string PeriodKey,
    DateTime From,
    DateTime To,
    decimal Debit,
    decimal Credit,
    decimal Difference,
    string Status);

public sealed record FinalAccountsReportExport(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record FinalAccountsStatementTemplateDto(
    string TemplateCode,
    string Version,
    string Name,
    string StatementType,
    string DefaultView,
    string DefaultRoundingUnit,
    bool HideZeroDefault,
    IReadOnlyList<FinalAccountsStatementTemplateNodeDto> Nodes);

public sealed record FinalAccountsStatementTemplateNodeDto(
    string Key,
    string Label,
    string NodeType,
    int SortOrder,
    string? ParentKey,
    string? Formula,
    string SignRule,
    bool Required,
    bool DrillDown,
    string Note,
    string ScheduleReference,
    string MappingRule);

public sealed record FinalAccountsProfitLossReportResponse(
    FinalAccountsStatementTemplateDto Template,
    string View,
    string RoundingUnit,
    DateTime? From,
    DateTime? To,
    bool HideZero,
    decimal Revenue,
    decimal GrossProfit,
    decimal Ebitda,
    decimal ProfitBeforeTax,
    decimal ProfitAfterTax,
    IReadOnlyList<FinalAccountsProfitLossLineDto> Lines,
    IReadOnlyList<FinalAccountsProfitLossHorizontalDto> Horizontal,
    IReadOnlyList<FinalAccountsValidationIssueDto> MappingIssues);

public sealed record FinalAccountsProfitLossLineDto(
    string Key,
    string Label,
    string NodeType,
    int SortOrder,
    string? ParentKey,
    string SignRule,
    decimal Current,
    decimal Previous,
    decimal Variance,
    decimal? VariancePercent,
    decimal? PercentOfSales,
    string Note,
    string ScheduleReference,
    string DrillDownPath,
    IReadOnlyList<FinalAccountsStatementMappingDto> Mappings);

public sealed record FinalAccountsProfitLossHorizontalDto(
    string Metric,
    decimal Current,
    decimal Previous,
    decimal Variance,
    decimal? VariancePercent);

public sealed record FinalAccountsStatementMappingDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid? GroupId,
    string GroupName,
    decimal Current,
    decimal Previous,
    string DrillDownPath);
