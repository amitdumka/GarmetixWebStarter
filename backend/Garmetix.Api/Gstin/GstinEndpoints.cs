using Garmetix.Api.Auth;

namespace Garmetix.Api.Gstin;

public static class GstinEndpoints
{
    public static RouteGroupBuilder MapGstinEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/gstin")
            .WithTags("GSTIN")
            .RequireAuthorization();

        group.MapGet("/{gstin}", async (string gstin, GstinLookupService lookup, CancellationToken cancellationToken) =>
            Results.Ok(await lookup.LookupAsync(gstin, cancellationToken)));

        group.MapPost("/lookup", async (GstinLookupRequest request, GstinLookupService lookup, CancellationToken cancellationToken) =>
            Results.Ok(await lookup.LookupAsync(request.Gstin, cancellationToken)));

        group.MapPost("/validate-party", async (PartyGstinValidationRequest request, GstinLookupService lookup, CancellationToken cancellationToken) =>
            Results.Ok(await lookup.ValidatePartyAsync(request.PartyType, request.Gstin, request.Name, request.Address, cancellationToken)));

        return group;
    }
}
