using Garmetix.Api.Workspace;
using Garmetix.Core.Models.Accounting;
using Garmetix.Core.Models.FinalAccounts;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsDirectLedgerIntegrationService(
    GarmetixDbContext db,
    FinalAccountsReportService reports)
{
    public async Task<FinalAccountsDirectLedgerIntegrationResponse> PreviewAsync(
        FinalAccountsDirectLedgerIntegrationQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var scope = ResolveScope(context, query.CompanyId, query.StoreGroupId, query.StoreId);
        var from = query.From?.Date;
        var to = query.To?.Date;
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new InvalidOperationException("From date must be on or before To date.");
        }

        var asOf = (query.AsOf ?? to ?? DateTime.UtcNow).Date;
        var ledgerGroups = await ApplyCompanyScope(WorkspaceScope.ApplyTo(db.LedgerGroups.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var ledgers = await ApplyCompanyScope(WorkspaceScope.ApplyTo(db.Ledgers.AsNoTracking(), context).Where(item => !item.Deleted), scope)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var journalEntries = ApplyScope(WorkspaceScope.ApplyTo(db.JournalEntries.AsNoTracking(), context).Where(item => !item.Deleted && item.Posted), scope);
        var journalLines = ApplyScope(WorkspaceScope.ApplyTo(db.JournalLines.AsNoTracking(), context).Where(item => !item.Deleted), scope);
        var allRowsQuery =
            from line in journalLines
            join entry in journalEntries on line.JournalEntryId equals entry.Id
            select new CanonicalJournalRow(
                entry.Id,
                line.Id,
                line.LedgerId,
                entry.SourceType,
                entry.OnDate,
                line.Debit,
                line.Credit);

        var allRows = await allRowsQuery.ToListAsync(cancellationToken);
        var periodRows = allRows
            .Where(item => (!from.HasValue || item.OnDate.Date >= from.Value) && (!to.HasValue || item.OnDate.Date <= to.Value))
            .ToList();
        var beforeFromRows = from.HasValue
            ? allRows.Where(item => item.OnDate.Date < from.Value).ToList()
            : [];
        var upToAsOfRows = allRows.Where(item => item.OnDate.Date <= asOf).ToList();

        var groupClassifications = BuildGroupClassifications(ledgerGroups.Values, ledgers);
        var trialRows = BuildTrialBalanceRows(ledgers, ledgerGroups, beforeFromRows, periodRows, upToAsOfRows);
        var sourceTypes = BuildSourceTypes(periodRows);
        var directProfitLoss = BuildDirectProfitLoss(trialRows);
        var directBalanceSheet = BuildDirectBalanceSheet(trialRows, directProfitLoss.GetValueOrDefault("ProfitAfterTax"));

        var finalAccountsTrialBalance = await reports.GetTrialBalanceAsync(
            new FinalAccountsTrialBalanceReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, from, to, "Ledger", true, "None"),
            context,
            cancellationToken);
        var finalAccountsProfitLoss = await reports.GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, from, to, "Vertical", "Ones", false),
            context,
            cancellationToken);
        var finalAccountsBalanceSheet = await reports.GetBalanceSheetAsync(
            new FinalAccountsBalanceSheetReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, asOf, null, "Proprietorship", "Ones", false),
            context,
            cancellationToken);

        var comparisons = BuildStatementComparisons(trialRows, directProfitLoss, directBalanceSheet, finalAccountsTrialBalance, finalAccountsProfitLoss, finalAccountsBalanceSheet);
        var activeMappingCount = await db.FinalAccountsAccountMappings.AsNoTracking()
            .Where(item => !item.Deleted && item.IsActive && item.CompanyId == scope.CompanyId && item.StoreGroupId == scope.StoreGroupId && item.StoreId == scope.StoreId)
            .CountAsync(cancellationToken);
        var issues = BuildIssues(ledgers.Count, periodRows.Count, groupClassifications, sourceTypes, comparisons, activeMappingCount);
        var summary = BuildSummary(ledgers.Count, ledgerGroups.Count, periodRows, trialRows, directProfitLoss, directBalanceSheet, issues, activeMappingCount);

        return new FinalAccountsDirectLedgerIntegrationResponse(
            DateTimeOffset.UtcNow,
            scope,
            FinalAccountsDirectLedgerIntegrationRules.StageName,
            WritesData: false,
            FinalAccountsDirectLedgerIntegrationRules.BackupRequirement,
            summary,
            trialRows,
            comparisons,
            groupClassifications,
            sourceTypes,
            issues,
            FinalAccountsDirectLedgerIntegrationRules.IntegrationPlan,
            FinalAccountsDirectLedgerIntegrationRules.RollbackPlan);
    }

    private static IReadOnlyList<FinalAccountsDirectLedgerGroupClassificationDto> BuildGroupClassifications(IEnumerable<LedgerGroup> groups, IReadOnlyList<Ledger> ledgers)
        => groups
            .OrderBy(item => item.Name)
            .Select(group =>
            {
                var classification = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup(group.Name, group.Category);
                return new FinalAccountsDirectLedgerGroupClassificationDto(
                    group.Id,
                    group.Name,
                    group.Category.ToString(),
                    ledgers.Count(ledger => ledger.LedgerGroupId == group.Id),
                    classification.PrimaryGroup,
                    classification.AccountType.ToString(),
                    classification.NaturalBalance.ToString(),
                    classification.Confidence,
                    classification.RuleCode,
                    FinalAccountsDirectLedgerIntegrationRules.ClassificationStatus(classification.Confidence, classification.RuleCode),
                    FinalAccountsDirectLedgerIntegrationRules.SuggestedActionForClassification(classification.Confidence, classification.RuleCode));
            })
            .ToList();

    private static IReadOnlyList<FinalAccountsDirectLedgerTrialBalanceDto> BuildTrialBalanceRows(
        IReadOnlyList<Ledger> ledgers,
        IReadOnlyDictionary<Guid, LedgerGroup> ledgerGroups,
        IReadOnlyList<CanonicalJournalRow> beforeFromRows,
        IReadOnlyList<CanonicalJournalRow> periodRows,
        IReadOnlyList<CanonicalJournalRow> upToAsOfRows)
        => ledgers.Select(ledger =>
        {
            ledgerGroups.TryGetValue(ledger.LedgerGroupId, out var group);
            var classification = FinalAccountsCoaNormalizationRules.ClassifyLedgerGroup(group?.Name ?? "Unmapped", group?.Category ?? 0);
            var openingSigned = ledger.OpeningBalance + beforeFromRows.Where(row => row.LedgerId == ledger.Id).Sum(row => row.Debit - row.Credit);
            var periodDebit = periodRows.Where(row => row.LedgerId == ledger.Id).Sum(row => row.Debit);
            var periodCredit = periodRows.Where(row => row.LedgerId == ledger.Id).Sum(row => row.Credit);
            var closingSigned = ledger.OpeningBalance + upToAsOfRows.Where(row => row.LedgerId == ledger.Id).Sum(row => row.Debit - row.Credit);
            var opening = FinalAccountsReportRules.SplitSignedBalance(FinalAccountsReportRules.RoundAmount(openingSigned));
            var closing = FinalAccountsReportRules.SplitSignedBalance(FinalAccountsReportRules.RoundAmount(closingSigned));
            var statementCategory = classification.AccountType is FinalAccountsAccountType.Income or FinalAccountsAccountType.Expense
                ? FinalAccountsStatementRules.ClassifyProfitLossCategory(VirtualAccount(ledger, classification), group?.Name)
                : FinalAccountsStatementRules.ClassifyBalanceSheetCategory(VirtualAccount(ledger, classification), group?.Name);

            return new FinalAccountsDirectLedgerTrialBalanceDto(
                ledger.Id,
                ledger.Name,
                ledger.LedgerGroupId,
                group?.Name ?? "Missing ledger group",
                classification.AccountType.ToString(),
                classification.NaturalBalance.ToString(),
                opening.Debit,
                opening.Credit,
                FinalAccountsReportRules.RoundAmount(periodDebit),
                FinalAccountsReportRules.RoundAmount(periodCredit),
                closing.Debit,
                closing.Credit,
                FinalAccountsDirectLedgerIntegrationRules.BalanceType(closingSigned),
                statementCategory,
                classification.RuleCode);
        }).ToList();

    private static IReadOnlyList<FinalAccountsDirectLedgerSourceTypeDto> BuildSourceTypes(IReadOnlyList<CanonicalJournalRow> rows)
        => rows
            .GroupBy(item => string.IsNullOrWhiteSpace(item.SourceType) ? "Manual" : item.SourceType.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Key)
            .Select(group =>
            {
                var debit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.Debit));
                var credit = FinalAccountsReportRules.RoundAmount(group.Sum(item => item.Credit));
                return new FinalAccountsDirectLedgerSourceTypeDto(
                    group.Key,
                    group.Select(item => item.JournalEntryId).Distinct().Count(),
                    group.Select(item => item.JournalLineId).Distinct().Count(),
                    debit,
                    credit,
                    FinalAccountsReportRules.RoundAmount(debit - credit),
                    FinalAccountsDirectLedgerIntegrationRules.StatusForDifference(debit - credit));
            })
            .ToList();

    private static Dictionary<string, decimal> BuildDirectProfitLoss(IReadOnlyList<FinalAccountsDirectLedgerTrialBalanceDto> trialRows)
    {
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in trialRows.Where(item => item.AccountType is "Income" or "Expense" && !string.IsNullOrWhiteSpace(item.StatementCategory)))
        {
            var movement = row.AccountType == "Income"
                ? row.PeriodCredit - row.PeriodDebit
                : row.PeriodDebit - row.PeriodCredit;
            values[row.StatementCategory] = FinalAccountsReportRules.RoundAmount(values.GetValueOrDefault(row.StatementCategory) + movement);
        }

        foreach (var node in FinalAccountsStatementRules.ProfitLossTemplate().Nodes.Where(item => string.Equals(item.NodeType, "Formula", StringComparison.OrdinalIgnoreCase)))
        {
            values[node.Key] = FinalAccountsStatementRules.EvaluateFormula(node.Formula ?? string.Empty, values);
        }

        return values;
    }

    private static Dictionary<string, decimal> BuildDirectBalanceSheet(IReadOnlyList<FinalAccountsDirectLedgerTrialBalanceDto> trialRows, decimal currentYearProfit)
    {
        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["CurrentYearProfit"] = currentYearProfit
        };

        foreach (var row in trialRows.Where(item => item.AccountType is not ("Income" or "Expense") && !string.IsNullOrWhiteSpace(item.StatementCategory)))
        {
            var signed = row.ClosingDebit - row.ClosingCredit;
            var amount = row.AccountType is "Asset" or "ContraLiability" ? signed : -signed;
            values[row.StatementCategory] = FinalAccountsReportRules.RoundAmount(values.GetValueOrDefault(row.StatementCategory) + amount);
        }

        foreach (var node in FinalAccountsStatementRules.BalanceSheetTemplate("Proprietorship").Nodes.Where(item => string.Equals(item.NodeType, "Formula", StringComparison.OrdinalIgnoreCase)))
        {
            if (node.Key == "CurrentYearProfit")
            {
                continue;
            }

            values[node.Key] = FinalAccountsStatementRules.EvaluateFormula(node.Formula ?? string.Empty, values);
        }

        return values;
    }

    private static IReadOnlyList<FinalAccountsDirectLedgerStatementComparisonDto> BuildStatementComparisons(
        IReadOnlyList<FinalAccountsDirectLedgerTrialBalanceDto> trialRows,
        IReadOnlyDictionary<string, decimal> directProfitLoss,
        IReadOnlyDictionary<string, decimal> directBalanceSheet,
        FinalAccountsTrialBalanceReportResponse finalAccountsTrialBalance,
        FinalAccountsProfitLossReportResponse finalAccountsProfitLoss,
        FinalAccountsBalanceSheetReportResponse finalAccountsBalanceSheet)
    {
        var directPeriodDebit = FinalAccountsReportRules.RoundAmount(trialRows.Sum(item => item.PeriodDebit));
        var directPeriodCredit = FinalAccountsReportRules.RoundAmount(trialRows.Sum(item => item.PeriodCredit));
        var directClosingDebit = FinalAccountsReportRules.RoundAmount(trialRows.Sum(item => item.ClosingDebit));
        var directClosingCredit = FinalAccountsReportRules.RoundAmount(trialRows.Sum(item => item.ClosingCredit));
        var directTotalAssets = directBalanceSheet.GetValueOrDefault("TotalAssets");
        var directTotalLiabilities = directBalanceSheet.GetValueOrDefault("CurrentLiabilities") + directBalanceSheet.GetValueOrDefault("NonCurrentLiabilities");
        var directTotalEquity = directBalanceSheet.GetValueOrDefault("CapitalEquity") + directBalanceSheet.GetValueOrDefault("CurrentYearProfit");
        var directBalanceDifference = FinalAccountsReportRules.RoundAmount(directTotalAssets - directTotalLiabilities - directTotalEquity);

        return
        [
            Comparison("Trial Balance", "Period debit", directPeriodDebit, finalAccountsTrialBalance.TotalPeriodDebit, "Books JournalLines vs Final Accounts journal period debit."),
            Comparison("Trial Balance", "Period credit", directPeriodCredit, finalAccountsTrialBalance.TotalPeriodCredit, "Books JournalLines vs Final Accounts journal period credit."),
            Comparison("Trial Balance", "Closing debit", directClosingDebit, finalAccountsTrialBalance.TotalClosingDebit, "Books Ledger closing debit vs Final Accounts closing debit."),
            Comparison("Trial Balance", "Closing credit", directClosingCredit, finalAccountsTrialBalance.TotalClosingCredit, "Books Ledger closing credit vs Final Accounts closing credit."),
            Comparison("Profit & Loss", "Revenue", directProfitLoss.GetValueOrDefault("Revenue"), finalAccountsProfitLoss.Revenue, "Books income ledgers classified directly vs Final Accounts P&L revenue."),
            Comparison("Profit & Loss", "Profit after tax", directProfitLoss.GetValueOrDefault("ProfitAfterTax"), finalAccountsProfitLoss.ProfitAfterTax, "Books income/expense ledgers classified directly vs Final Accounts P&L PAT."),
            Comparison("Balance Sheet", "Total assets", directTotalAssets, finalAccountsBalanceSheet.TotalAssets, "Books asset ledgers classified directly vs Final Accounts Balance Sheet assets."),
            Comparison("Balance Sheet", "Total liabilities", directTotalLiabilities, finalAccountsBalanceSheet.TotalLiabilities, "Books liability ledgers classified directly vs Final Accounts Balance Sheet liabilities."),
            Comparison("Balance Sheet", "Total equity", directTotalEquity, finalAccountsBalanceSheet.TotalEquity, "Books equity ledgers plus current profit vs Final Accounts Balance Sheet equity."),
            Comparison("Balance Sheet", "Books equality difference", directBalanceDifference, finalAccountsBalanceSheet.Difference, "Books direct Balance Sheet equality difference vs Final Accounts difference.")
        ];
    }

    private static FinalAccountsDirectLedgerStatementComparisonDto Comparison(string statement, string metric, decimal booksValue, decimal finalAccountsValue, string notes)
    {
        var difference = FinalAccountsReportRules.RoundAmount(booksValue - finalAccountsValue);
        return new(statement, metric, booksValue, finalAccountsValue, difference, FinalAccountsDirectLedgerIntegrationRules.StatusForDifference(difference), notes);
    }

    private static IReadOnlyList<FinalAccountsDirectLedgerIssueDto> BuildIssues(
        int ledgerCount,
        int journalLineCount,
        IReadOnlyList<FinalAccountsDirectLedgerGroupClassificationDto> groups,
        IReadOnlyList<FinalAccountsDirectLedgerSourceTypeDto> sourceTypes,
        IReadOnlyList<FinalAccountsDirectLedgerStatementComparisonDto> comparisons,
        int activeMappingCount)
    {
        var issues = new List<FinalAccountsDirectLedgerIssueDto>();
        AddIssue(issues, "Error", "NoBooksLedgers", ledgerCount == 0 ? 1 : 0, "No canonical Books ledgers were found in scope.", "Create or migrate Books ledgers before direct Final Accounts integration.");
        AddIssue(issues, "Warning", "NoBooksJournalLines", journalLineCount == 0 ? 1 : 0, "No posted Books journal lines were found for the selected period.", "Confirm source modules are posting to Books accounting before switching reports.");
        AddIssue(issues, "Error", "ManualLedgerGroupClassification", groups.Count(item => item.Status == "ManualReview"), "Books ledger groups need manual classification.", "Resolve low-confidence/unmapped groups with Amit/CA.");
        AddIssue(issues, "Error", "UnbalancedSourceType", sourceTypes.Count(item => item.Status == "Difference"), "One or more Books journal source types are not balanced.", "Fix source posting before direct statement integration.");
        AddIssue(issues, "Error", "StatementDifference", comparisons.Count(item => item.Status == "Difference"), "Canonical Books ledger values differ from Final Accounts statement values.", "Do not switch report source until differences are explained or fixed.");
        AddIssue(issues, "Warning", "ExceptionMappingsRemain", activeMappingCount, "Active Final Accounts mapping rows still exist.", "Keep mappings only for true import/exception gaps after direct ledger integration.");
        return issues;
    }

    private static IReadOnlyList<FinalAccountsDirectLedgerSummaryDto> BuildSummary(
        int ledgerCount,
        int ledgerGroupCount,
        IReadOnlyList<CanonicalJournalRow> periodRows,
        IReadOnlyList<FinalAccountsDirectLedgerTrialBalanceDto> trialRows,
        IReadOnlyDictionary<string, decimal> profitLoss,
        IReadOnlyDictionary<string, decimal> balanceSheet,
        IReadOnlyList<FinalAccountsDirectLedgerIssueDto> issues,
        int activeMappingCount)
    {
        var directAssets = balanceSheet.GetValueOrDefault("TotalAssets");
        var directLiabilities = balanceSheet.GetValueOrDefault("CurrentLiabilities") + balanceSheet.GetValueOrDefault("NonCurrentLiabilities");
        var directEquity = balanceSheet.GetValueOrDefault("CapitalEquity") + balanceSheet.GetValueOrDefault("CurrentYearProfit");
        return
        [
            new("Books ledger groups", ledgerGroupCount, "Canonical Books ledger groups in scope."),
            new("Books ledgers", ledgerCount, "Canonical Books ledgers in scope."),
            new("Posted journal lines", periodRows.Count, "Canonical Books journal lines in the selected period."),
            new("Trial Balance period debit", trialRows.Sum(item => item.PeriodDebit), "Books journal-line debit total."),
            new("Trial Balance period credit", trialRows.Sum(item => item.PeriodCredit), "Books journal-line credit total."),
            new("Profit after tax", profitLoss.GetValueOrDefault("ProfitAfterTax"), "Direct P&L computed from canonical Books ledgers."),
            new("Balance Sheet assets", directAssets, "Direct Balance Sheet assets from canonical Books ledgers."),
            new("Balance Sheet liabilities and equity", directLiabilities + directEquity, "Direct liabilities, equity and current profit from canonical Books ledgers."),
            new("Active exception mappings", activeMappingCount, "Mappings should become exception-only after approval."),
            new("Blocking issues", issues.Count(item => item.Severity == "Error"), "Must be zero before switching report source.")
        ];
    }

    private static FinalAccountsAccount VirtualAccount(Ledger ledger, CoaClassificationResult classification)
        => new()
        {
            Id = ledger.Id,
            Code = ledger.Id.ToString("N")[..8],
            Name = ledger.Name,
            AccountType = classification.AccountType,
            NaturalBalance = classification.NaturalBalance
        };

    private static void AddIssue(List<FinalAccountsDirectLedgerIssueDto> issues, string severity, string code, int count, string message, string suggestedAction)
    {
        if (count > 0)
        {
            issues.Add(new(severity, code, count, message, suggestedAction));
        }
    }

    private static FinalAccountsScopeDto ResolveScope(HttpContext context, Guid? companyId, Guid? storeGroupId, Guid? storeId)
    {
        if (WorkspaceScope.HasFullAccess(context))
        {
            return new FinalAccountsScopeDto(companyId, storeGroupId, storeId);
        }

        return new FinalAccountsScopeDto(
            WorkspaceScope.ClaimGuid(context, "companyId"),
            WorkspaceScope.ClaimGuid(context, "storeGroupId"),
            WorkspaceScope.ClaimGuid(context, "storeId"));
    }

    private static IQueryable<T> ApplyScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope) where T : class
    {
        if (scope.CompanyId.HasValue)
        {
            query = query.Where(item => EF.Property<Guid>(item, "CompanyId") == scope.CompanyId.Value);
        }

        if (scope.StoreGroupId.HasValue)
        {
            query = query.Where(item => EF.Property<Guid>(item, "StoreGroupId") == scope.StoreGroupId.Value);
        }

        if (scope.StoreId.HasValue)
        {
            query = query.Where(item => EF.Property<Guid>(item, "StoreId") == scope.StoreId.Value);
        }

        return query;
    }

    private static IQueryable<T> ApplyCompanyScope<T>(IQueryable<T> query, FinalAccountsScopeDto scope) where T : class
    {
        if (scope.CompanyId.HasValue)
        {
            query = query.Where(item => EF.Property<Guid>(item, "CompanyId") == scope.CompanyId.Value);
        }

        return query;
    }

    private sealed record CanonicalJournalRow(
        Guid JournalEntryId,
        Guid JournalLineId,
        Guid LedgerId,
        string SourceType,
        DateTime OnDate,
        decimal Debit,
        decimal Credit);
}
