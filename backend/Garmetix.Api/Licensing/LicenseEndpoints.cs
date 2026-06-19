using System.Security.Claims;
using Garmetix.Api.Auth;

namespace Garmetix.Api.Licensing;

public static class LicenseEndpoints
{
    public static RouteGroupBuilder MapLicenseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/license")
            .WithTags("License Activation")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/status", Status);
        group.MapPost("/generate", Generate);
        group.MapPost("/activate", Activate);
        group.MapDelete("/activation", RemoveActivation);

        return group;
    }

    private static IResult Status(LicenseActivationService service)
        => Results.Ok(service.GetStatus());

    private static IResult Generate(LicenseGenerateRequest request, LicenseActivationService service)
    {
        try
        {
            return Results.Ok(service.Generate(request));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static IResult Activate(LicenseActivateRequest request, LicenseActivationService service, ClaimsPrincipal user)
    {
        try
        {
            var activatedBy = user.Identity?.Name
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue(ClaimTypes.Email)
                ?? "admin";
            return Results.Ok(service.Activate(request, activatedBy));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static IResult RemoveActivation(LicenseActivationService service)
        => Results.Ok(service.RemoveActivation());
}
