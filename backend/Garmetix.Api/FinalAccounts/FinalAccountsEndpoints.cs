using Garmetix.Api.Auth;

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
