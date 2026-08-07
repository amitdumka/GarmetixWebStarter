using Garmetix.Api.Auth;

namespace Garmetix.Api.SaleImport;

public static class VyaparCreditNoteImportEndpoints
{
    public static RouteGroupBuilder MapVyaparCreditNoteImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sale-import/vyapar-credit-note")
            .WithTags("Vyapar Credit Note Import")
            .RequireAuthorization(GarmetixPolicies.Admin);

        group.MapPost("/", async (
            VyaparCreditNoteImportRequest request,
            VyaparCreditNoteImportService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.ImportAsync(request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
        });

        return group;
    }
}
