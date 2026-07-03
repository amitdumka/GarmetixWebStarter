using Garmetix.Api.Auth;
using Garmetix.Api.Messages;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Onboarding;

public static class ClientOnboardingEndpoints
{
    public static RouteGroupBuilder MapClientOnboardingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/client-onboarding")
            .WithTags("Client Onboarding")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapGet("/options", OptionsAsync);
        group.MapGet("/summary", SummaryAsync);
        group.MapPost("/submit", SubmitAsync);

        return group;
    }

    private static async Task<IResult> OptionsAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var summary = await BuildSummaryAsync(db, cancellationToken);
        return Results.Ok(new ClientOnboardingOptionsDto(
            EnumOptions<Garmetix.Core.Enums.CompanyType>(),
            EnumOptions<Garmetix.Core.Enums.StoreCategory>(),
            EnumOptions<Garmetix.Core.Enums.AppOperation>(),
            EnumOptions<Garmetix.Core.Enums.Gender>(),
            summary,
            ClientOnboardingService.FlowSteps,
            ClientOnboardingService.ModelMappingNotes));
    }

    private static async Task<IResult> SummaryAsync(GarmetixDbContext db, CancellationToken cancellationToken)
        => Results.Ok(await BuildSummaryAsync(db, cancellationToken));

    private static async Task<IResult> SubmitAsync(
        ClientOnboardingRequest request,
        GarmetixDbContext db,
        ApplicationMessageLogService logs,
        CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        try
        {
            var service = new ClientOnboardingService(db);
            var response = await service.OnboardAsync(request, cancellationToken);
            await logs.SuccessAsync(
                "ClientOnboarding",
                "Submit",
                response.Message,
                new { response.Target, response.Created, response.Notes, response.LoginHints },
                response.Target.CompanyId,
                response.Target.StoreGroupId,
                response.Target.StoreId,
                resource: "client-onboarding/submit",
                operationId: operationId,
                cancellationToken: cancellationToken);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            await logs.ErrorAsync(
                "ClientOnboarding",
                "Submit",
                ex.Message,
                new
                {
                    request.CompanyConfig.ClientCode,
                    request.CompanyConfig.GroupCode,
                    request.CompanyConfig.StoreCode,
                    request.CompanyDetails.CompanyName,
                    request.CompanyDetails.GSTIN,
                    request.ClientDetails.Email
                },
                resource: "client-onboarding/submit",
                operationId: operationId,
                cancellationToken: cancellationToken);
            return Results.BadRequest(new { message = ex.Message, operationId });
        }
        catch (Exception ex)
        {
            await logs.ErrorAsync(
                "ClientOnboarding",
                "Submit",
                "Unexpected error while onboarding company.",
                new { ex.Message, ExceptionType = ex.GetType().FullName, ex.StackTrace },
                resource: "client-onboarding/submit",
                operationId: operationId,
                cancellationToken: cancellationToken);
            return Results.Problem("Unexpected error while onboarding company. Check Message Logs for details.", statusCode: 500);
        }
    }

    private static async Task<ClientOnboardingSummaryDto> BuildSummaryAsync(GarmetixDbContext db, CancellationToken cancellationToken)
    {
        var companyCount = await db.Companies.AsNoTracking().CountAsync(cancellationToken);
        var first = await db.Companies.AsNoTracking().OrderBy(item => item.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return new ClientOnboardingSummaryDto(
            companyCount,
            await db.StoreGroups.AsNoTracking().CountAsync(cancellationToken),
            await db.Stores.AsNoTracking().CountAsync(cancellationToken),
            companyCount > 0,
            first?.Name,
            first?.Code);
    }

    private static IReadOnlyList<EnumOptionDto> EnumOptions<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>()
            .Select(item => new EnumOptionDto(SplitName(item.ToString()), Convert.ToInt32(item), item.ToString()))
            .ToList();

    private static string SplitName(string value)
        => string.Concat(value.SelectMany((ch, index) => index > 0 && char.IsUpper(ch) ? new[] { ' ', ch } : new[] { ch })).Trim();
}
