using Garmetix.Api.Auth;

namespace Garmetix.Api.SaleImport;

public static class VyaparSaleImportEndpoints
{
    public static RouteGroupBuilder MapVyaparSaleImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sale-import/vyapar")
            .WithTags("Vyapar Sale Import")
            .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapPost("/preview", async (
            HttpContext context,
            IFormFile file,
            Guid companyId,
            Guid storeGroupId,
            Guid storeId,
            VyaparSaleImportService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var preview = await service.PreviewAsync(context, file, companyId, storeGroupId, storeId, cancellationToken);
                return Results.Ok(preview);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization(GarmetixPolicies.Billing);

        group.MapPost("/confirm", async (
            VyaparSaleImportConfirmRequest request,
            VyaparSaleImportService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.ConfirmAsync(context, request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/imported", async (
            HttpContext context,
            VyaparSaleImportService service,
            Guid companyId,
            Guid? storeId = null,
            DateTime? from = null,
            DateTime? to = null,
            string? q = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var result = await service.ListImportedAsync(context, companyId, storeId, from, to, q, page, pageSize, cancellationToken);
            return Results.Ok(result);
        });


        group.MapPost("/barcode-mapping/preview", async (
            HttpContext context,
            IFormFile file,
            VyaparSaleImportService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.PreviewBarcodeMappingUploadAsync(file, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        })
        .DisableAntiforgery()
        .RequireAuthorization(GarmetixPolicies.Billing);


        group.MapGet("/final-summary", async (
            HttpContext context,
            VyaparSaleImportService service,
            Guid companyId,
            Guid? storeId = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken cancellationToken = default) =>
        {
            try
            {
                var result = await service.GetFinalSummaryAsync(context, companyId, storeId, from, to, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Billing);

        group.MapGet("/batches", async (
            HttpContext context,
            VyaparSaleImportService service,
            Guid companyId,
            Guid? storeId = null,
            DateTime? from = null,
            DateTime? to = null,
            string? q = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default) =>
        {
            var result = await service.ListBatchesAsync(context, companyId, storeId, from, to, q, page, pageSize, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization(GarmetixPolicies.Billing);

        group.MapPost("/batches/{batchId:guid}/undo", async (
            Guid batchId,
            VyaparSaleImportUndoRequest request,
            VyaparSaleImportService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var actual = request with { BatchId = batchId };
                var result = await service.UndoBatchAsync(context, actual, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Delete);

        group.MapPost("/history/clear", async (
            VyaparSaleImportClearHistoryRequest request,
            VyaparSaleImportService service,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await service.ClearImportHistoryAsync(context, request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Delete);

        return group;
    }
}
