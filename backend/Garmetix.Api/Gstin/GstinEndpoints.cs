using Garmetix.Api.Auth;

namespace Garmetix.Api.Gstin;

public static class GstinEndpoints
{
    public static RouteGroupBuilder MapGstinEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gstin")
            .WithTags("GSTIN")
            .RequireAuthorization();

group.MapGet("/provider/status", (GstinLookupService lookup) =>
    Results.Ok(lookup.GetProviderStatus()))
    .RequireAuthorization(GarmetixPolicies.Admin);

group.MapPost("/provider/test", async (GstinProviderTestRequest request, GstinLookupService lookup, CancellationToken cancellationToken) =>
{
    var validation = await lookup.ValidatePartyAsync("Provider test", request.Gstin, request.PartyName, request.Address, cancellationToken);
    var success = validation.Lookup.IsValidFormat && (validation.Lookup.IsVerified || validation.Lookup.Status == "ProviderDisabled");
    var message = validation.Lookup.IsVerified
        ? "GSTIN provider returned verified legal/trade name data."
        : validation.Lookup.Status == "ProviderDisabled"
            ? "Local GSTIN validation passed, but the provider is disabled. Configure provider credentials for live lookup."
            : validation.Lookup.Message ?? "GSTIN lookup did not return verified data.";

    return Results.Ok(new GstinProviderTestResponse(success, message, validation.Lookup, validation.Alerts, DateTime.UtcNow));
})
.RequireAuthorization(GarmetixPolicies.Admin);

group.MapGet("/{gstin}", async (string gstin, GstinLookupService lookup, CancellationToken cancellationToken) =>
    Results.Ok(await lookup.LookupAsync(gstin, cancellationToken)));

        group.MapPost("/lookup", async (GstinLookupRequest request, GstinLookupService lookup, CancellationToken cancellationToken) =>
            Results.Ok(await lookup.LookupAsync(request.Gstin, cancellationToken)));

        group.MapPost("/validate-party", async (PartyGstinValidationRequest request, GstinLookupService lookup, CancellationToken cancellationToken) =>
            Results.Ok(await lookup.ValidatePartyAsync(request.PartyType, request.Gstin, request.Name, request.Address, cancellationToken)));

        return group;
    }
}
