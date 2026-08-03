using Garmetix.Api.Auth;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
            HttpContext context,
            VyaparSaleImportService service,
            IOptions<JsonOptions> jsonOptions,
            CancellationToken cancellationToken) =>
        {
            VyaparSaleImportConfirmRequest? request;
            try
            {
                request = await context.Request.ReadFromJsonAsync<VyaparSaleImportConfirmRequest>(
                    jsonOptions.Value.SerializerOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                // Minimal-API implicit body binding swallows deserialization failures as an empty 400.
                // Reading the body explicitly surfaces the real field/reason so a bad request is diagnosable
                // instead of showing up as a generic "invalid request" with no clue which invoice/field broke.
                return Results.BadRequest(new { message = $"Could not read the import request body: {ex.Message}" });
            }

            if (request is null)
            {
                return Results.BadRequest(new { message = "Import request body was empty." });
            }

            try
            {
                var result = await service.ConfirmAsync(context, request, cancellationToken);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // A raw SaveChanges failure (e.g. a Postgres column-length/constraint violation) previously
                // fell through to the generic global exception handler as an opaque "Unexpected server error"
                // with no clue which field or invoice caused it. Surfacing the real database message here
                // lets the frontend's per-chunk error enrichment show it next to the actual invoice numbers.
                var detail = ex.InnerException?.Message ?? ex.Message;
                return Results.Problem(
                    title: "Could not save this batch of invoices.",
                    detail: detail,
                    statusCode: StatusCodes.Status500InternalServerError);
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
