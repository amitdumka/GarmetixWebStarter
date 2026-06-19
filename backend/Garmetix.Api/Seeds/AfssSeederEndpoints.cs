using Garmetix.Api.Auth;
using Garmetix.Api.Messages;
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

    private static async Task<IResult> SeedAsync(
        AfssSeedRequest request,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();

        if (string.IsNullOrWhiteSpace(request.ProfileCode))
        {
            const string message = "Select a seed profile.";
            await logs.ErrorAsync("AFSSSeeder", "Seed", message, request, companyId: request.CompanyId, resource: "afss-seeder/seed", operationId: operationId, cancellationToken: cancellationToken);
            return Results.BadRequest(new { message, operationId });
        }

        try
        {
            var service = new AfssDefaultSeederService(db);
            var response = await service.SeedAsync(request, cancellationToken);
            await logs.SuccessAsync(
                "AFSSSeeder",
                "Seed",
                response.Message,
                new { response.Target, response.Created, response.Notes, request.IncludeUsers, request.IncludeEmployees, request.IncludeProducts, request.ResetDefaultUserPasswords },
                response.Target.CompanyId,
                response.Target.StoreGroupId,
                response.Target.StoreId,
                resource: "afss-seeder/seed",
                operationId: operationId,
                cancellationToken: cancellationToken);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            await logs.ErrorAsync("AFSSSeeder", "Seed", ex.Message, request, companyId: request.CompanyId, resource: "afss-seeder/seed", operationId: operationId, cancellationToken: cancellationToken);
            return Results.BadRequest(new { message = ex.Message, operationId });
        }
        catch (Exception ex)
        {
            await logs.ErrorAsync(
                "AFSSSeeder",
                "Seed",
                "Unexpected error while running AF/SS seed.",
                new { request, ex.Message, ExceptionType = ex.GetType().FullName, ex.StackTrace },
                companyId: request.CompanyId,
                resource: "afss-seeder/seed",
                operationId: operationId,
                cancellationToken: cancellationToken);
            return Results.Problem("Unexpected error while running AF/SS seed. Check Message Logs for details.", statusCode: 500);
        }
    }
}
