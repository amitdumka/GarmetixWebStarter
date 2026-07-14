using Garmetix.Api.Auth;
using Garmetix.Core.Models.FinalAccounts;

namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsEndpoints
{
    public static RouteGroupBuilder MapFinalAccountsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(FinalAccountsSettingsDefaults.ApiRoot)
            .WithTags("Final Accounts")
            .RequireAuthorization(GarmetixPolicies.FinalAccounts);

        group.MapGet("/status", GetStatusAsync);
        group.MapGet("/settings", GetSettingsAsync);
        group.MapPut("/settings", SaveSettingsAsync).RequireAuthorization(GarmetixPolicies.Admin);
        group.MapGet("/audit/accounting-master", GetAccountingMasterAuditAsync);
        group.MapGet("/audit/coa-normalization", GetCoaNormalizationPreviewAsync);
        group.MapGet("/audit/party-ledger-unification", GetPartyLedgerUnificationPreviewAsync);
        group.MapGet("/audit/transaction-backfill-reconciliation", GetTransactionBackfillReconciliationPreviewAsync);

        var enabled = group.MapGroup("")
            .AddEndpointFilter<FinalAccountsEnabledFilter>();

        enabled.MapGet("/dashboard", GetDashboardPlaceholderAsync);
        enabled.MapGet("/account-groups", ListAccountGroupsAsync);
        enabled.MapPost("/account-groups", CreateAccountGroupAsync);
        enabled.MapPut("/account-groups/{id:guid}", UpdateAccountGroupAsync);
        enabled.MapDelete("/account-groups/{id:guid}", DeleteAccountGroupAsync);
        enabled.MapGet("/accounts", ListAccountsAsync);
        enabled.MapGet("/accounts/search", SearchAccountsAsync);
        enabled.MapPost("/accounts", CreateAccountAsync);
        enabled.MapPut("/accounts/{id:guid}", UpdateAccountAsync);
        enabled.MapDelete("/accounts/{id:guid}", DeleteAccountAsync);
        enabled.MapGet("/account-mappings", ListAccountMappingsAsync);
        enabled.MapPost("/account-mappings", CreateAccountMappingAsync);
        enabled.MapPut("/account-mappings/{id:guid}", UpdateAccountMappingAsync);
        enabled.MapDelete("/account-mappings/{id:guid}", DeleteAccountMappingAsync);
        enabled.MapGet("/fiscal-years", ListFiscalYearsAsync);
        enabled.MapPost("/fiscal-years", CreateFiscalYearAsync);
        enabled.MapPut("/fiscal-years/{id:guid}", UpdateFiscalYearAsync);
        enabled.MapGet("/fiscal-periods", ListFiscalPeriodsAsync);
        enabled.MapPost("/fiscal-periods", CreateFiscalPeriodAsync);
        enabled.MapPut("/fiscal-periods/{id:guid}", UpdateFiscalPeriodAsync);
        enabled.MapGet("/journals", ListJournalsAsync);
        enabled.MapGet("/journals/{id:guid}", GetJournalAsync);
        enabled.MapPost("/journals/preview", PreviewJournalAsync);
        enabled.MapPost("/journals", CreateJournalDraftAsync);
        enabled.MapPut("/journals/{id:guid}", UpdateJournalDraftAsync);
        enabled.MapPost("/journals/{id:guid}/post", PostJournalAsync);
        enabled.MapPost("/journals/{id:guid}/reverse", ReverseJournalAsync);
        enabled.MapDelete("/journals/{id:guid}", DeleteJournalDraftAsync);
        enabled.MapGet("/posting-rules", ListPostingRulesAsync);
        enabled.MapGet("/posting-rules/mapping-validation", GetMappingValidationAsync);
        enabled.MapPost("/posting/preview", PreviewPostingAsync);
        enabled.MapGet("/posting/adapters", ListPostingAdaptersAsync);
        enabled.MapGet("/posting/adapters/{adapterKey}/sources/{sourceId:guid}/preview", PreviewAdapterPostingAsync);
        enabled.MapGet("/sync/options", GetSyncOptionsAsync);
        enabled.MapGet("/sync/jobs", ListSyncJobsAsync);
        enabled.MapPost("/sync/manual", CreateManualSyncJobAsync);
        enabled.MapDelete("/sync/jobs/{id:guid}/dry-run", CleanupDryRunSyncJobAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/backfill/dry-run", DryRunBackfillAsync);
        enabled.MapPost("/reconciliation/summary", GetReconciliationAsync);
        enabled.MapPost("/reconciliation/export", ExportReconciliationAsync);
        enabled.MapGet("/reports/general-ledger", GetGeneralLedgerReportAsync);
        enabled.MapGet("/reports/general-ledger/export", ExportGeneralLedgerReportAsync);
        enabled.MapGet("/reports/trial-balance", GetTrialBalanceReportAsync);
        enabled.MapGet("/reports/trial-balance/export", ExportTrialBalanceReportAsync);
        enabled.MapGet("/reports/profit-loss", GetProfitLossReportAsync);
        enabled.MapGet("/reports/profit-loss/export", ExportProfitLossReportAsync);
        enabled.MapGet("/reports/balance-sheet", GetBalanceSheetReportAsync);
        enabled.MapGet("/reports/balance-sheet/export", ExportBalanceSheetReportAsync);
        enabled.MapGet("/reports/cash-flow", GetCashFlowReportAsync);
        enabled.MapGet("/reports/cash-flow/export", ExportCashFlowReportAsync);
        enabled.MapGet("/reports/schedules", GetSchedulesReportAsync);
        enabled.MapGet("/reports/schedules/export", ExportSchedulesReportAsync);
        enabled.MapGet("/ca/adjustments", ListCaAdjustmentsAsync);
        enabled.MapGet("/ca/adjustments/{id:guid}", GetCaAdjustmentAsync);
        enabled.MapPost("/ca/adjustments/preview", PreviewCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/ca/adjustments", CreateCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPut("/ca/adjustments/{id:guid}", UpdateCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/ca/adjustments/{id:guid}/preview", PreviewSavedCaAdjustmentAsync);
        enabled.MapPost("/ca/adjustments/{id:guid}/submit", SubmitCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/ca/adjustments/{id:guid}/review", ReviewCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/ca/adjustments/{id:guid}/approve", ApproveCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/ca/adjustments/{id:guid}/reject", RejectCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/ca/adjustments/{id:guid}/post", PostCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/ca/adjustments/{id:guid}/reverse", ReverseCaAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/ca/adjustments/{id:guid}/comments", AddCaAdjustmentCommentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/ca/adjustments/{id:guid}/attachments", AddCaAdjustmentAttachmentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapGet("/ca/statement-line-comments", ListStatementLineCommentsAsync);
        enabled.MapPost("/ca/statement-line-comments", AddStatementLineCommentAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapGet("/ca/report-versions", ListReportVersionsAsync);
        enabled.MapPost("/ca/report-versions", CreateReportVersionAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapGet("/period-close/runs", ListCloseRunsAsync);
        enabled.MapGet("/period-close/runs/{id:guid}", GetCloseRunAsync);
        enabled.MapPost("/period-close/preview", PreviewCloseAsync);
        enabled.MapPost("/period-close/close", CommitCloseAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/period-close/runs/{id:guid}/reopen", ReopenCloseRunAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapGet("/projections/scenarios", ListProjectionScenariosAsync);
        enabled.MapGet("/projections/scenarios/{id:guid}", GetProjectionScenarioAsync);
        enabled.MapPost("/projections/scenarios", CreateProjectionScenarioAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPut("/projections/scenarios/{id:guid}", UpdateProjectionScenarioAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/projections/scenarios/{id:guid}/clone", CloneProjectionScenarioAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/projections/scenarios/{id:guid}/submit", SubmitProjectionScenarioAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/projections/scenarios/{id:guid}/approve", ApproveProjectionScenarioAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/projections/scenarios/{id:guid}/archive", ArchiveProjectionScenarioAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapPost("/projections/baseline/actuals", ImportProjectionActualBaselineAsync);
        enabled.MapPost("/projections/compare", CompareProjectionScenariosAsync);
        enabled.MapGet("/projections/scenarios/{id:guid}/export", ExportProjectionScenarioAsync);
        enabled.MapGet("/tally/profiles", ListTallyProfilesAsync);
        enabled.MapPost("/tally/profiles", CreateTallyProfileAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPut("/tally/profiles/{id:guid}", UpdateTallyProfileAsync).RequireAuthorization(GarmetixPolicies.Edit);
        enabled.MapPost("/tally/preview", PreviewTallyExchangeAsync);
        enabled.MapPost("/tally/export", ExportTallyExchangeAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapGet("/exchange/runs", ListExchangeRunsAsync);
        enabled.MapPost("/ca-package/preview", PreviewCaPackageAsync);
        enabled.MapPost("/ca-package/export", ExportCaPackageAsync).RequireAuthorization(GarmetixPolicies.Admin);
        enabled.MapGet("/coa/seed-preview", GetSeedPreviewAsync);
        enabled.MapGet("/validation/summary", GetValidationSummaryAsync);

        return group;
    }

    private static async Task<IResult> GetStatusAsync(
        FinalAccountsSettingsService settings,
        HttpContext context,
        CancellationToken cancellationToken)
        => Results.Ok(await settings.GetAsync(context, cancellationToken));

    private static async Task<IResult> GetSettingsAsync(
        FinalAccountsSettingsService settings,
        HttpContext context,
        CancellationToken cancellationToken)
        => Results.Ok(await settings.GetAsync(context, cancellationToken));

    private static async Task<IResult> SaveSettingsAsync(
        FinalAccountsSaveSettingsRequest request,
        FinalAccountsSettingsService settings,
        HttpContext context,
        CancellationToken cancellationToken)
        => Results.Ok(await settings.SaveAsync(request, context, cancellationToken));

    private static Task<IResult> GetDashboardPlaceholderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IResult>(Results.Ok(new
        {
            module = "Final Accounts",
            status = "Enabled",
            message = "Final Accounts dashboard endpoint is reserved for BS-02 and later stages."
        }));
    }

    private static Task<IResult> GetAccountingMasterAuditAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAccountingMasterAuditService audit,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => audit.RunAsync(new FinalAccountsAccountingMasterAuditQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> GetCoaNormalizationPreviewAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCoaNormalizationService normalization,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => normalization.PreviewAsync(new FinalAccountsCoaNormalizationPreviewQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> GetPartyLedgerUnificationPreviewAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsPartyLedgerUnificationService unification,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => unification.PreviewAsync(new FinalAccountsPartyLedgerUnificationPreviewQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> GetTransactionBackfillReconciliationPreviewAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? modules,
        FinalAccountsTransactionBackfillReconciliationService reconciliation,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reconciliation.PreviewAsync(new FinalAccountsTransactionBackfillReconciliationQuery(companyId, storeGroupId, storeId, from, to, modules), context, cancellationToken));

    private static Task<IResult> ListAccountGroupsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.ListAccountGroupsAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateAccountGroupAsync(
        FinalAccountsAccountGroupRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.CreateAccountGroupAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateAccountGroupAsync(
        Guid id,
        FinalAccountsAccountGroupRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.UpdateAccountGroupAsync(id, request, context, cancellationToken));

    private static Task<IResult> DeleteAccountGroupAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleNoContentAsync(() => catalog.DeleteAccountGroupAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ListAccountsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.ListAccountsAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> SearchAccountsAsync(
        string? term,
        int? take,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.SearchAccountsAsync(term, take, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateAccountAsync(
        FinalAccountsAccountRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.CreateAccountAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateAccountAsync(
        Guid id,
        FinalAccountsAccountRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.UpdateAccountAsync(id, request, context, cancellationToken));

    private static Task<IResult> DeleteAccountAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleNoContentAsync(() => catalog.DeleteAccountAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ListAccountMappingsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.ListAccountMappingsAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateAccountMappingAsync(
        FinalAccountsAccountMappingRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.CreateAccountMappingAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateAccountMappingAsync(
        Guid id,
        FinalAccountsAccountMappingRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.UpdateAccountMappingAsync(id, request, context, cancellationToken));

    private static Task<IResult> DeleteAccountMappingAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleNoContentAsync(() => catalog.DeleteAccountMappingAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ListFiscalYearsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.ListFiscalYearsAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateFiscalYearAsync(
        FinalAccountsFiscalYearRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.CreateFiscalYearAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateFiscalYearAsync(
        Guid id,
        FinalAccountsFiscalYearRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.UpdateFiscalYearAsync(id, request, context, cancellationToken));

    private static Task<IResult> ListFiscalPeriodsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.ListFiscalPeriodsAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateFiscalPeriodAsync(
        FinalAccountsFiscalPeriodRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.CreateFiscalPeriodAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateFiscalPeriodAsync(
        Guid id,
        FinalAccountsFiscalPeriodRequest request,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.UpdateFiscalPeriodAsync(id, request, context, cancellationToken));

    private static Task<IResult> ListJournalsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? status,
        string? sourceType,
        int? page,
        int? pageSize,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.ListJournalsAsync(new FinalAccountsJournalQuery(companyId, storeGroupId, storeId, from, to, status, sourceType, page, pageSize), context, cancellationToken));

    private static Task<IResult> GetJournalAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.GetJournalAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> PreviewJournalAsync(
        FinalAccountsJournalSaveRequest request,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.PreviewJournalAsync(request, context, cancellationToken));

    private static Task<IResult> CreateJournalDraftAsync(
        FinalAccountsJournalSaveRequest request,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.CreateDraftAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateJournalDraftAsync(
        Guid id,
        FinalAccountsJournalSaveRequest request,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.UpdateDraftAsync(id, request, context, cancellationToken));

    private static Task<IResult> PostJournalAsync(
        Guid id,
        FinalAccountsJournalPostRequest? request,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.PostJournalAsync(id, request ?? new FinalAccountsJournalPostRequest(null), new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ReverseJournalAsync(
        Guid id,
        FinalAccountsJournalReverseRequest request,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => journals.ReverseJournalAsync(id, request, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> DeleteJournalDraftAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsJournalService journals,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleNoContentAsync(() => journals.DeleteDraftAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ListPostingRulesAsync(
        string? sourceType,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsPostingRuleService postingRules,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => postingRules.ListPostingRulesAsync(sourceType, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> GetMappingValidationAsync(
        string? sourceType,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsPostingRuleService postingRules,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => postingRules.GetMappingValidationAsync(sourceType, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> PreviewPostingAsync(
        FinalAccountsPostingPreviewRequest request,
        FinalAccountsPostingRuleService postingRules,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => postingRules.PreviewPostingAsync(request, context, cancellationToken));

    private static Task<IResult> ListPostingAdaptersAsync(
        FinalAccountsPostingAdapterService adapters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IResult>(Results.Ok(adapters.ListAdapters()));
    }

    private static Task<IResult> PreviewAdapterPostingAsync(
        string adapterKey,
        Guid sourceId,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsPostingAdapterService adapters,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => adapters.PreviewAsync(adapterKey, sourceId, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> GetSyncOptionsAsync(
        FinalAccountsSyncService sync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IResult>(Results.Ok(sync.GetOptions()));
    }

    private static Task<IResult> ListSyncJobsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsSyncService sync,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => sync.ListJobsAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateManualSyncJobAsync(
        FinalAccountsBackfillRequest request,
        FinalAccountsSyncService sync,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => sync.CreateManualSyncJobAsync(request, context, cancellationToken));

    private static Task<IResult> CleanupDryRunSyncJobAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsSyncService sync,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleNoContentAsync(() => sync.CleanupDryRunJobAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> DryRunBackfillAsync(
        FinalAccountsBackfillRequest request,
        FinalAccountsSyncService sync,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => sync.DryRunBackfillAsync(request, context, cancellationToken));

    private static Task<IResult> GetReconciliationAsync(
        FinalAccountsBackfillRequest request,
        FinalAccountsSyncService sync,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => sync.GetReconciliationAsync(request, context, cancellationToken));

    private static async Task<IResult> ExportReconciliationAsync(
        FinalAccountsBackfillRequest request,
        FinalAccountsSyncService sync,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await sync.GetReconciliationAsync(request, context, cancellationToken);
            var lines = new List<string> { "Module,SourceTotal,JournalDebit,JournalCredit,Difference,SourceCount,PostedLinkCount,ExceptionCount,Status" };
            lines.AddRange(model.Modules.Select(item => string.Join(",",
                EscapeCsv(item.Module),
                item.SourceTotal.ToString("0.00"),
                item.JournalDebit.ToString("0.00"),
                item.JournalCredit.ToString("0.00"),
                item.Difference.ToString("0.00"),
                item.SourceCount.ToString(),
                item.PostedLinkCount.ToString(),
                item.ExceptionCount.ToString(),
                EscapeCsv(item.Status))));
            return Results.Text(string.Join(Environment.NewLine, lines), "text/csv");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetGeneralLedgerReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        Guid? accountId,
        DateTime? from,
        DateTime? to,
        bool? includeReversed,
        int? page,
        int? pageSize,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reports.GetGeneralLedgerAsync(
            new FinalAccountsGeneralLedgerReportQuery(companyId, storeGroupId, storeId, accountId, from, to, includeReversed, page, pageSize),
            context,
            cancellationToken));

    private static async Task<IResult> ExportGeneralLedgerReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        Guid? accountId,
        DateTime? from,
        DateTime? to,
        bool? includeReversed,
        string? format,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await reports.ExportGeneralLedgerAsync(
                new FinalAccountsGeneralLedgerReportQuery(companyId, storeGroupId, storeId, accountId, from, to, includeReversed, 1, null),
                format,
                context,
                cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetTrialBalanceReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? view,
        bool? includeZeroBalances,
        string? comparison,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reports.GetTrialBalanceAsync(
            new FinalAccountsTrialBalanceReportQuery(companyId, storeGroupId, storeId, from, to, view, includeZeroBalances, comparison),
            context,
            cancellationToken));

    private static async Task<IResult> ExportTrialBalanceReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? view,
        bool? includeZeroBalances,
        string? comparison,
        string? format,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await reports.ExportTrialBalanceAsync(
                new FinalAccountsTrialBalanceReportQuery(companyId, storeGroupId, storeId, from, to, view, includeZeroBalances, comparison),
                format,
                context,
                cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetProfitLossReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? view,
        string? roundingUnit,
        bool? hideZero,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reports.GetProfitLossAsync(
            new FinalAccountsProfitLossReportQuery(companyId, storeGroupId, storeId, from, to, view, roundingUnit, hideZero),
            context,
            cancellationToken));

    private static async Task<IResult> ExportProfitLossReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? view,
        string? roundingUnit,
        bool? hideZero,
        string? format,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await reports.ExportProfitLossAsync(
                new FinalAccountsProfitLossReportQuery(companyId, storeGroupId, storeId, from, to, view, roundingUnit, hideZero),
                format,
                context,
                cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetBalanceSheetReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? asOf,
        DateTime? previousAsOf,
        string? entityType,
        string? roundingUnit,
        bool? hideZero,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reports.GetBalanceSheetAsync(
            new FinalAccountsBalanceSheetReportQuery(companyId, storeGroupId, storeId, asOf, previousAsOf, entityType, roundingUnit, hideZero),
            context,
            cancellationToken));

    private static async Task<IResult> ExportBalanceSheetReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? asOf,
        DateTime? previousAsOf,
        string? entityType,
        string? roundingUnit,
        bool? hideZero,
        string? format,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await reports.ExportBalanceSheetAsync(
                new FinalAccountsBalanceSheetReportQuery(companyId, storeGroupId, storeId, asOf, previousAsOf, entityType, roundingUnit, hideZero),
                format,
                context,
                cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetCashFlowReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? roundingUnit,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reports.GetCashFlowAsync(
            new FinalAccountsCashFlowReportQuery(companyId, storeGroupId, storeId, from, to, roundingUnit),
            context,
            cancellationToken));

    private static async Task<IResult> ExportCashFlowReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? from,
        DateTime? to,
        string? roundingUnit,
        string? format,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await reports.ExportCashFlowAsync(
                new FinalAccountsCashFlowReportQuery(companyId, storeGroupId, storeId, from, to, roundingUnit),
                format,
                context,
                cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetSchedulesReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? asOf,
        string? schedule,
        bool? includeZeroBalances,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => reports.GetSchedulesAsync(
            new FinalAccountsSchedulesReportQuery(companyId, storeGroupId, storeId, asOf, schedule, includeZeroBalances),
            context,
            cancellationToken));

    private static async Task<IResult> ExportSchedulesReportAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        DateTime? asOf,
        string? schedule,
        bool? includeZeroBalances,
        string? format,
        FinalAccountsReportService reports,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await reports.ExportSchedulesAsync(
                new FinalAccountsSchedulesReportQuery(companyId, storeGroupId, storeId, asOf, schedule, includeZeroBalances),
                format,
                context,
                cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> ListCaAdjustmentsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? status,
        DateTime? from,
        DateTime? to,
        int? page,
        int? pageSize,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.ListAdjustmentsAsync(
            new FinalAccountsAdjustmentQuery(companyId, storeGroupId, storeId, status, from, to, page, pageSize),
            context,
            cancellationToken));

    private static Task<IResult> GetCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.GetAdjustmentAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> PreviewCaAdjustmentAsync(
        FinalAccountsAdjustmentSaveRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.PreviewAdjustmentAsync(request, context, cancellationToken));

    private static Task<IResult> PreviewSavedCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.PreviewAdjustmentAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateCaAdjustmentAsync(
        FinalAccountsAdjustmentSaveRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.CreateAdjustmentAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateCaAdjustmentAsync(
        Guid id,
        FinalAccountsAdjustmentSaveRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.UpdateAdjustmentAsync(id, request, context, cancellationToken));

    private static Task<IResult> SubmitCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentWorkflowRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveCaAdjustmentAsync(id, companyId, storeGroupId, storeId, FinalAccountsAdjustmentStatus.Submitted, request, ca, context, cancellationToken);

    private static Task<IResult> ReviewCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentWorkflowRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveCaAdjustmentAsync(id, companyId, storeGroupId, storeId, FinalAccountsAdjustmentStatus.Review, request, ca, context, cancellationToken);

    private static Task<IResult> ApproveCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentWorkflowRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveCaAdjustmentAsync(id, companyId, storeGroupId, storeId, FinalAccountsAdjustmentStatus.Approved, request, ca, context, cancellationToken);

    private static Task<IResult> RejectCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentWorkflowRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => MoveCaAdjustmentAsync(id, companyId, storeGroupId, storeId, FinalAccountsAdjustmentStatus.Rejected, request, ca, context, cancellationToken);

    private static Task<IResult> MoveCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentStatus status,
        FinalAccountsAdjustmentWorkflowRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.MoveAdjustmentAsync(id, status, request, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> PostCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentPostRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.PostAdjustmentAsync(id, request, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ReverseCaAdjustmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentReverseRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.ReverseAdjustmentAsync(id, request, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> AddCaAdjustmentCommentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentCommentRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.AddCommentAsync(id, request, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> AddCaAdjustmentAttachmentAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsAdjustmentAttachmentRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.AddAttachmentAsync(id, request, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> ListStatementLineCommentsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? statementType,
        string? statementLineKey,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.ListStatementLineCommentsAsync(companyId, storeGroupId, storeId, statementType, statementLineKey, context, cancellationToken));

    private static Task<IResult> AddStatementLineCommentAsync(
        FinalAccountsStatementLineCommentRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.AddStatementLineCommentAsync(request, context, cancellationToken));

    private static Task<IResult> ListReportVersionsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? reportType,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.ListReportVersionsAsync(companyId, storeGroupId, storeId, reportType, context, cancellationToken));

    private static Task<IResult> CreateReportVersionAsync(
        FinalAccountsReportVersionRequest request,
        FinalAccountsCaWorkspaceService ca,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => ca.CreateReportVersionAsync(request, context, cancellationToken));

    private static Task<IResult> ListCloseRunsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        Guid? fiscalYearId,
        Guid? fiscalPeriodId,
        string? status,
        int? page,
        int? pageSize,
        FinalAccountsPeriodCloseService periodClose,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => periodClose.ListRunsAsync(
            new FinalAccountsCloseRunQuery(companyId, storeGroupId, storeId, fiscalYearId, fiscalPeriodId, status, page, pageSize),
            context,
            cancellationToken));

    private static Task<IResult> GetCloseRunAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsPeriodCloseService periodClose,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => periodClose.GetRunAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> PreviewCloseAsync(
        FinalAccountsClosePreviewRequest request,
        FinalAccountsPeriodCloseService periodClose,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => periodClose.PreviewCloseAsync(request, context, cancellationToken));

    private static Task<IResult> CommitCloseAsync(
        FinalAccountsCloseCommitRequest request,
        FinalAccountsPeriodCloseService periodClose,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => periodClose.CommitCloseAsync(request, context, cancellationToken));

    private static Task<IResult> ReopenCloseRunAsync(
        Guid id,
        FinalAccountsCloseReopenRequest request,
        FinalAccountsPeriodCloseService periodClose,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => periodClose.ReopenAsync(id, request, context, cancellationToken));

    private static Task<IResult> ListProjectionScenariosAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? status,
        string? scenarioType,
        int? page,
        int? pageSize,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.ListScenariosAsync(new FinalAccountsProjectionScenarioQuery(companyId, storeGroupId, storeId, status, scenarioType, page, pageSize), context, cancellationToken));

    private static Task<IResult> GetProjectionScenarioAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.GetScenarioAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> CreateProjectionScenarioAsync(
        FinalAccountsProjectionScenarioSaveRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.CreateScenarioAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateProjectionScenarioAsync(
        Guid id,
        FinalAccountsProjectionScenarioSaveRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.UpdateScenarioAsync(id, request, context, cancellationToken));

    private static Task<IResult> CloneProjectionScenarioAsync(
        Guid id,
        FinalAccountsProjectionCloneRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.CloneScenarioAsync(id, request, context, cancellationToken));

    private static Task<IResult> SubmitProjectionScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.SubmitScenarioAsync(id, request, context, cancellationToken));

    private static Task<IResult> ApproveProjectionScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.ApproveScenarioAsync(id, request, context, cancellationToken));

    private static Task<IResult> ArchiveProjectionScenarioAsync(
        Guid id,
        FinalAccountsProjectionWorkflowRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.ArchiveScenarioAsync(id, request, context, cancellationToken));

    private static Task<IResult> ImportProjectionActualBaselineAsync(
        FinalAccountsProjectionActualBaselineRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.ImportActualBaselineAsync(request, context, cancellationToken));

    private static Task<IResult> CompareProjectionScenariosAsync(
        FinalAccountsProjectionCompareRequest request,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => projections.CompareAsync(request, context, cancellationToken));

    private static async Task<IResult> ExportProjectionScenarioAsync(
        Guid id,
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? format,
        FinalAccountsProjectionService projections,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await projections.ExportScenarioAsync(id, new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), format, context, cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> ListTallyProfilesAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        bool? activeOnly,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => exchange.ListProfilesAsync(new FinalAccountsTallyProfileQuery(companyId, storeGroupId, storeId, activeOnly), context, cancellationToken));

    private static Task<IResult> CreateTallyProfileAsync(
        FinalAccountsTallyProfileRequest request,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => exchange.CreateProfileAsync(request, context, cancellationToken));

    private static Task<IResult> UpdateTallyProfileAsync(
        Guid id,
        FinalAccountsTallyProfileRequest request,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => exchange.UpdateProfileAsync(id, request, context, cancellationToken));

    private static Task<IResult> PreviewTallyExchangeAsync(
        FinalAccountsExchangeRequest request,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => exchange.PreviewTallyAsync(request, context, cancellationToken));

    private static async Task<IResult> ExportTallyExchangeAsync(
        FinalAccountsExchangeRequest request,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await exchange.ExportTallyAsync(request, context, cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> ListExchangeRunsAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        string? runKind,
        int? page,
        int? pageSize,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => exchange.ListRunsAsync(new FinalAccountsExchangeRunQuery(companyId, storeGroupId, storeId, runKind, page, pageSize), context, cancellationToken));

    private static Task<IResult> PreviewCaPackageAsync(
        FinalAccountsCaPackageRequest request,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => exchange.PreviewCaPackageAsync(request, context, cancellationToken));

    private static async Task<IResult> ExportCaPackageAsync(
        FinalAccountsCaPackageRequest request,
        FinalAccountsExchangeService exchange,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await exchange.ExportCaPackageAsync(request, context, cancellationToken);
            return Results.File(export.Content, export.ContentType, export.FileName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static Task<IResult> GetSeedPreviewAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.GetSeedPreviewAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static Task<IResult> GetValidationSummaryAsync(
        Guid? companyId,
        Guid? storeGroupId,
        Guid? storeId,
        FinalAccountsCatalogService catalog,
        HttpContext context,
        CancellationToken cancellationToken)
        => HandleAsync(() => catalog.GetValidationSummaryAsync(new FinalAccountsCatalogQuery(companyId, storeGroupId, storeId), context, cancellationToken));

    private static async Task<IResult> HandleAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static async Task<IResult> HandleNoContentAsync(Func<Task> action)
    {
        try
        {
            await action();
            return Results.NoContent();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return ToErrorResult(ex);
        }
    }

    private static IResult ToErrorResult(Exception ex)
        => ex switch
        {
            ArgumentException => Results.BadRequest(new { error = ex.Message }),
            InvalidOperationException => Results.Conflict(new { error = ex.Message }),
            KeyNotFoundException => Results.NotFound(new { error = ex.Message }),
            _ => Results.Problem(ex.Message)
        };

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
