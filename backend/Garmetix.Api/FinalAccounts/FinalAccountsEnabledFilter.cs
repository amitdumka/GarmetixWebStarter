namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsEnabledFilter(FinalAccountsSettingsService settings) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (await settings.IsEnabledAsync(context.HttpContext, context.HttpContext.RequestAborted))
        {
            return await next(context);
        }

        return Results.Json(new
        {
            message = "Final Accounts module is disabled for this workspace.",
            featureKey = FinalAccountsSettingsDefaults.FeatureKey,
            setupPath = "/final-accounts/setup"
        }, statusCode: StatusCodes.Status403Forbidden);
    }
}
