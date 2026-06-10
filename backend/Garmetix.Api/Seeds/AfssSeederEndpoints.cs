using Garmetix.Api.Auth;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Seeds;

public static class AfssSeederEndpoints
{
    public static RouteGroupBuilder MapAfssSeederEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/afss-seeder")
            .WithTags("AF/SS Seeder")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/options", OptionsAsync);
        group.MapGet("/analysis", () => Results.Ok(AfssDefaultSeederService.Comparison));
        group.MapPost("/seed", SeedAsync);

        return group;
    }

    private static async Task<IResult> OptionsAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var companies = await db.Companies
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .Select(item => new AfssSeedCompanyDto(
                item.Id,
                item.Name,
                item.Code,
                item.GSTIN,
                item.ContactPerson,
                db.StoreGroups.Count(group => group.CompanyId == item.Id),
                db.Stores.Count(store => store.CompanyId == item.Id)))
            .ToListAsync(cancellationToken);

        return Results.Ok(new AfssSeederOptionsDto(AfssDefaultSeederService.Profiles, companies, AfssDefaultSeederService.Comparison));
    }

    private static async Task<IResult> SeedAsync(AfssSeedRequest request, GarmetixDbContext db, CancellationToken cancellationToken)
    {
        if (request.CompanyId == Guid.Empty)
        {
            return Results.BadRequest(new { message = "Select a company before running AF/SS seed." });
        }

        if (string.IsNullOrWhiteSpace(request.ProfileCode))
        {
            return Results.BadRequest(new { message = "Select a seed profile." });
        }

        try
        {
            var service = new AfssDefaultSeederService(db);
            var response = await service.SeedAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
