namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsTransactionBackfillReconciliationService(
    FinalAccountsSyncService sync,
    FinalAccountsReportService reports)
{
    public async Task<FinalAccountsTransactionBackfillReconciliationResponse> PreviewAsync(
        FinalAccountsTransactionBackfillReconciliationQuery query,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var modules = FinalAccountsTransactionBackfillReconciliationRules.ParseModules(query.Modules);
        var backfillRequest = new FinalAccountsBackfillRequest(
            query.CompanyId,
            query.StoreGroupId,
            query.StoreId,
            query.From?.Date,
            query.To?.Date,
            modules,
            StopOnError: true,
            IdempotencyKey: null);

        var dryRun = await sync.DryRunBackfillAsync(backfillRequest, context, cancellationToken);
        var reconciliation = await sync.GetReconciliationAsync(backfillRequest, context, cancellationToken);
        var scope = new FinalAccountsScopeDto(dryRun.CompanyId, dryRun.StoreGroupId, dryRun.StoreId);
        var statementAsOf = (query.To ?? DateTime.UtcNow).Date;
        var from = query.From?.Date;

        var trialBalance = await reports.GetTrialBalanceAsync(
            new FinalAccountsTrialBalanceReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, from, query.To?.Date, "Ledger", true, "None"),
            context,
            cancellationToken);
        var balanceSheet = await reports.GetBalanceSheetAsync(
            new FinalAccountsBalanceSheetReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, statementAsOf, null, "Proprietorship", "Ones", false),
            context,
            cancellationToken);
        var profitLoss = await reports.GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(scope.CompanyId, scope.StoreGroupId, scope.StoreId, from, query.To?.Date, "Vertical", "Ones", false),
            context,
            cancellationToken);

        var statementEvidence = new FinalAccountsTransactionBackfillStatementEvidenceDto(
            from,
            statementAsOf,
            trialBalance.Status,
            trialBalance.Difference,
            balanceSheet.Status,
            balanceSheet.Difference,
            profitLoss.Revenue,
            profitLoss.ProfitAfterTax,
            profitLoss.MappingIssues.Count,
            trialBalance.Diagnostics.Count,
            balanceSheet.Diagnostics.Count);

        var moduleEvidence = BuildModuleEvidence(dryRun, reconciliation);
        var issues = BuildIssues(dryRun, reconciliation, statementEvidence, moduleEvidence);
        var summary = BuildSummary(dryRun, reconciliation, statementEvidence, issues);

        return new FinalAccountsTransactionBackfillReconciliationResponse(
            DateTimeOffset.UtcNow,
            scope,
            FinalAccountsTransactionBackfillReconciliationRules.StageName,
            WritesData: false,
            FinalAccountsTransactionBackfillReconciliationRules.BackupRequirement,
            summary,
            dryRun,
            reconciliation,
            statementEvidence,
            moduleEvidence,
            issues.OrderByDescending(item => SeverityRank(item.Severity)).ThenBy(item => item.Code).ToList(),
            FinalAccountsTransactionBackfillReconciliationRules.ApprovalGates,
            FinalAccountsTransactionBackfillReconciliationRules.RollbackPlan);
    }

    private static IReadOnlyList<FinalAccountsTransactionBackfillModuleEvidenceDto> BuildModuleEvidence(
        FinalAccountsBackfillPreviewResponse dryRun,
        FinalAccountsReconciliationResponse reconciliation)
    {
        var reconciliationByModule = reconciliation.Modules.ToDictionary(item => item.Module, StringComparer.OrdinalIgnoreCase);
        return dryRun.ModulePreviews.Select(module =>
        {
            reconciliationByModule.TryGetValue(module.Module, out var reconciled);
            var difference = reconciled?.Difference ?? FinalAccountsJournalRules.RoundAmount(module.SourceTotal - Math.Max(module.LinkedJournalDebit, module.LinkedJournalCredit));
            var exceptionCount = reconciled?.ExceptionCount ?? 0;
            var status = FinalAccountsTransactionBackfillReconciliationRules.StatusForDifference(difference, exceptionCount, module.DriftCount);
            return new FinalAccountsTransactionBackfillModuleEvidenceDto(
                module.Module,
                module.SourceCount,
                module.PendingCount,
                module.DriftCount,
                reconciled?.PostedLinkCount ?? module.AlreadyLinkedCount,
                module.SourceTotal,
                reconciled?.JournalDebit ?? module.LinkedJournalDebit,
                reconciled?.JournalCredit ?? module.LinkedJournalCredit,
                difference,
                exceptionCount,
                status,
                FinalAccountsTransactionBackfillReconciliationRules.SuggestedActionForModule(status, module.PendingCount));
        }).OrderBy(item => item.Module, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<FinalAccountsTransactionBackfillIssueDto> BuildIssues(
        FinalAccountsBackfillPreviewResponse dryRun,
        FinalAccountsReconciliationResponse reconciliation,
        FinalAccountsTransactionBackfillStatementEvidenceDto statements,
        IReadOnlyList<FinalAccountsTransactionBackfillModuleEvidenceDto> modules)
    {
        var issues = new List<FinalAccountsTransactionBackfillIssueDto>();
        AddIssue(issues, "Warning", "PendingBackfillRows", dryRun.PendingCount, "Source rows are not linked to Final Accounts posting links.", "Run approved dry-run batches only after BS-17/BS-18 mapping reviews are approved.");
        AddIssue(issues, "Error", "SourceHashDrift", dryRun.DriftCount, "Previously-linked source rows have hash drift.", "Review source document changes and reverse/repost only with approval.");
        AddIssue(issues, "Error", "ReconciliationDifference", Math.Abs(reconciliation.Difference) > 0.01m ? 1 : 0, "Source totals and linked journal totals do not reconcile.", "Do not backfill live until the difference is explained.");
        AddIssue(issues, "Warning", "OpenSyncExceptions", reconciliation.ExceptionCount, "Unresolved Final Accounts sync exceptions exist.", "Resolve or approve exceptions before live posting.");
        AddIssue(issues, "Error", "ModuleDifferences", modules.Count(item => item.Status == "Difference"), "One or more modules have source/journal differences.", "Review module evidence before any mutation.");
        AddIssue(issues, "Error", "TrialBalanceDifference", Math.Abs(statements.TrialBalanceDifference) > 0.01m ? 1 : 0, "Trial Balance debit and credit totals are not equal.", "Fix posting/mapping before close or live direct integration.");
        AddIssue(issues, "Error", "BalanceSheetDifference", Math.Abs(statements.BalanceSheetDifference) > 0.01m ? 1 : 0, "Balance Sheet is not equal.", "Fix ledger classification/posting before any live backfill.");
        AddIssue(issues, "Warning", "ProfitLossMappingIssues", statements.ProfitLossMappingIssueCount, "Profit & Loss has mapping issues.", "Review P&L mappings with Amit/CA before using as final evidence.");
        return issues;
    }

    private static IReadOnlyList<FinalAccountsTransactionBackfillSummaryDto> BuildSummary(
        FinalAccountsBackfillPreviewResponse dryRun,
        FinalAccountsReconciliationResponse reconciliation,
        FinalAccountsTransactionBackfillStatementEvidenceDto statements,
        IReadOnlyList<FinalAccountsTransactionBackfillIssueDto> issues)
        =>
        [
            new("Source rows", "Info", dryRun.SourceCount, "Source transactions included in the read-only dry-run."),
            new("Pending rows", dryRun.PendingCount == 0 ? "Balanced" : "Review", dryRun.PendingCount, "Rows not linked to Final Accounts source posting links."),
            new("Drift rows", dryRun.DriftCount == 0 ? "Balanced" : "Error", dryRun.DriftCount, "Linked rows where source hash changed."),
            new("Source total", "Info", dryRun.SourceTotal, "Total source amount across selected modules."),
            new("Reconciliation difference", Math.Abs(reconciliation.Difference) <= 0.01m ? "Balanced" : "Error", reconciliation.Difference, "Source total minus linked journal control total."),
            new("Trial Balance difference", statements.TrialBalanceStatus, statements.TrialBalanceDifference, "Final Accounts Trial Balance control."),
            new("Balance Sheet difference", statements.BalanceSheetStatus, statements.BalanceSheetDifference, "Final Accounts Balance Sheet control."),
            new("Profit after tax", "Info", statements.ProfitLossProfitAfterTax, "Profit & Loss tie-out figure for the selected period."),
            new("Blocking issues", issues.Any(item => item.Severity == "Error") ? "Error" : "Balanced", issues.Count(item => item.Severity == "Error"), "Errors that block live mutation.")
        ];

    private static void AddIssue(List<FinalAccountsTransactionBackfillIssueDto> issues, string severity, string code, int count, string message, string suggestedAction)
    {
        if (count > 0)
        {
            issues.Add(new FinalAccountsTransactionBackfillIssueDto(severity, code, count, message, suggestedAction));
        }
    }

    private static int SeverityRank(string severity)
        => severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ? 3
            : severity.Equals("Warning", StringComparison.OrdinalIgnoreCase) ? 2
            : 1;
}
