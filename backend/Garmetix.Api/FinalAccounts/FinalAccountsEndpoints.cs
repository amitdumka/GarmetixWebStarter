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
}
