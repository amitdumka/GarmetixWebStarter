namespace Garmetix.Api.Auth;

public static class AccessMatrixEndpoints
{
    public static RouteGroupBuilder MapAccessMatrixEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/access")
            .WithTags("Access")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/matrix", () => Results.Ok(AccessPermissionMatrix.Profiles));

        return group;
    }
}
