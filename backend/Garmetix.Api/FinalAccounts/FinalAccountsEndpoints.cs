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
}
