using Garmetix.Api.Auth;

namespace Garmetix.Api.PurchaseImport;

public static class PurchaseInvoiceImportEndpoints
{
    public static RouteGroupBuilder MapPurchaseInvoiceImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/purchase-import")
            .WithTags("Purchase Invoice Import")
            .RequireAuthorization(GarmetixPolicies.Purchase);

        group.MapGet("/batches", async (PurchaseInvoiceImportService service, HttpContext context, int take, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(context, take <= 0 ? 50 : take, cancellationToken)));

        group.MapGet("/batches/{id:guid}", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var batch = await service.GetBatchAsync(context, id, cancellationToken);
            return batch is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(batch);
        });

        group.MapGet("/batches/{id:guid}/posting-report", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var report = await service.GetPostingReportAsync(context, id, cancellationToken);
            return report is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(report);
        });


        group.MapPost("/batches/{id:guid}/acceptance", async (Guid id, PurchaseInvoiceImportAcceptanceUpdateRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var batch = await service.UpdateAcceptanceAsync(context, id, request, cancellationToken);
                return batch is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(batch);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/batches/{id:guid}/correction-safety", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var safety = await service.GetCorrectionSafetyAsync(context, id, cancellationToken);
            return safety is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(safety);
        });

        group.MapGet("/batches/{id:guid}/correction-plan", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var plan = await service.GetCorrectionPlanAsync(context, id, cancellationToken);
            return plan is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(plan);
        });

        group.MapPost("/batches/{id:guid}/request-correction", async (Guid id, PurchaseInvoiceImportCorrectionRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var safety = await service.RequestCorrectionAsync(context, id, request, cancellationToken);
                return safety is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(safety);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);


        group.MapGet("/batches/{id:guid}/audit-json", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var export = await service.ExportBatchAuditJsonAsync(context, id, cancellationToken);
            return export is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.File(export.Value.Bytes, export.Value.ContentType, export.Value.FileName);
        });

        group.MapGet("/batches/{id:guid}/lines-csv", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var export = await service.ExportBatchLinesCsvAsync(context, id, cancellationToken);
            return export is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.File(export.Value.Bytes, export.Value.ContentType, export.Value.FileName);
        });

        group.MapDelete("/batches/{id:guid}", async (Guid id, bool? deleteFiles, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var deleted = await service.DeleteBatchAsync(context, id, deleteFiles.GetValueOrDefault(true), cancellationToken);
                return deleted ? Results.Ok(new { id, deleted = true, filesDeleted = deleteFiles.GetValueOrDefault(true) }) : Results.NotFound(new { message = "Supplier invoice import draft was not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapPost("/cleanup-history", async (PurchaseInvoiceImportCleanupRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await service.CleanupHistoryAsync(context, request, cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/storage-summary", async (PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetStorageSummaryAsync(context, cancellationToken)));

        group.MapGet("/acceptance-summary", async (PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAcceptanceSummaryAsync(context, cancellationToken)));

        group.MapGet("/backup-restore-checklist", async (PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetBackupRestoreChecklistAsync(context, cancellationToken)));

        group.MapGet("/parser-qa-summary", async (PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetParserQaSummaryAsync(context, cancellationToken)));


        group.MapGet("/final-closure-status", async (PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetFinalClosureStatusAsync(context, cancellationToken)));

        group.MapGet("/parser-templates", (PurchaseInvoiceImportService service) =>
            Results.Ok(service.ListParserTemplates()));

        group.MapGet("/product-matches", async (PurchaseInvoiceImportService service, HttpContext context, string? query, Guid? storeId, int take, CancellationToken cancellationToken) =>
            Results.Ok(await service.SearchProductMatchesAsync(context, query, storeId, take <= 0 ? 15 : take, cancellationToken)));



        group.MapGet("/vendor-profiles", async (PurchaseInvoiceImportService service, HttpContext context, int take, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListVendorProfilesAsync(context, take <= 0 ? 100 : take, cancellationToken)));


        group.MapGet("/vendor-profiles/export", async (PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var export = await service.ExportVendorProfilesJsonAsync(context, cancellationToken);
            return Results.File(export.Bytes, export.ContentType, export.FileName);
        });

        group.MapGet("/vendor-profiles/{id:guid}", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var profile = await service.GetVendorProfileAsync(context, id, cancellationToken);
            return profile is null ? Results.NotFound(new { message = "Vendor invoice import profile was not found." }) : Results.Ok(profile);
        });

        group.MapPost("/vendor-profiles/{id:guid}/reset", async (Guid id, PurchaseInvoiceImportVendorProfileResetRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var profile = await service.ResetVendorProfileAsync(context, id, request, cancellationToken);
            return profile is null ? Results.NotFound(new { message = "Vendor invoice import profile was not found." }) : Results.Ok(profile);
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapPost("/vendor-profiles/{id:guid}/rules", async (Guid id, PurchaseInvoiceImportVendorProfileRulesRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var profile = await service.UpdateVendorProfileRulesAsync(context, id, request, cancellationToken);
            return profile is null ? Results.NotFound(new { message = "Vendor invoice import profile was not found." }) : Results.Ok(profile);
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapDelete("/vendor-profiles/{id:guid}", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var deleted = await service.DeleteVendorProfileAsync(context, id, cancellationToken);
            return deleted ? Results.Ok(new { id, deleted = true }) : Results.NotFound(new { message = "Vendor invoice import profile was not found." });
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapPost("/uploads", async (HttpRequest request, HttpContext context, PurchaseInvoiceImportService service, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Upload must be multipart/form-data." });
            }

            var form = await request.ReadFormAsync(cancellationToken);
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
            {
                return Results.BadRequest(new { message = "Supplier invoice file is required." });
            }

            if (!Guid.TryParse(form["companyId"].FirstOrDefault(), out var companyId) ||
                !Guid.TryParse(form["storeGroupId"].FirstOrDefault(), out var storeGroupId) ||
                !Guid.TryParse(form["storeId"].FirstOrDefault(), out var storeId))
            {
                return Results.BadRequest(new { message = "Company, store group, and store are required." });
            }

            try
            {
                var draft = await service.CreateUploadAsync(context, file, companyId, storeGroupId, storeId, form["rawText"].FirstOrDefault(), cancellationToken);
                return Results.Created($"/api/purchase-import/batches/{draft.Id}", draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).DisableAntiforgery();

        group.MapPut("/batches/{id:guid}", async (Guid id, PurchaseInvoiceImportUpdateRequest update, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.UpdateBatchAsync(context, id, update, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/lines/{lineId:guid}/match-product", async (Guid id, Guid lineId, PurchaseInvoiceImportProductMatchRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.ApplyProductMatchAsync(context, id, lineId, request, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/lines/{lineId:guid}/split-by-size", async (Guid id, Guid lineId, PurchaseInvoiceImportSplitLineRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.SplitLineBySizeAsync(context, id, lineId, request, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });



        group.MapPost("/batches/{id:guid}/generate-missing-barcodes", async (Guid id, PurchaseInvoiceImportGenerateBarcodeRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.GenerateMissingBarcodesAsync(context, id, request, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/override-duplicate", async (Guid id, PurchaseInvoiceImportDuplicateOverrideRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.OverrideDuplicateAsync(context, id, request, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapPost("/batches/{id:guid}/recheck-duplicate", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.RecheckDuplicateAsync(context, id, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/distribute-discount", async (Guid id, PurchaseInvoiceImportDistributeDiscountRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.DistributeHeaderDiscountAsync(context, id, request, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/reparse-text", async (Guid id, PurchaseInvoiceImportReparseTextRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var draft = await service.ReparseTextAsync(context, id, request, cancellationToken);
                return draft is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(draft);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/post", async (Guid id, PurchaseInvoiceImportPostRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var posted = await service.PostBatchAsync(context, id, request, cancellationToken);
                return posted is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(posted);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        group.MapPost("/batches/{id:guid}/reject", async (Guid id, PurchaseInvoiceImportRejectRequest request, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            try
            {
                var rejected = await service.RejectAsync(context, id, request.Reason, cancellationToken);
                return rejected ? Results.Ok(new { id, status = "Rejected" }) : Results.NotFound(new { message = "Supplier invoice import draft was not found." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).RequireAuthorization(GarmetixPolicies.Edit);

        group.MapGet("/batches/{id:guid}/files", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var files = await service.ListFilesAsync(context, id, cancellationToken);
            return files is null ? Results.NotFound(new { message = "Supplier invoice import draft was not found." }) : Results.Ok(files);
        });

        group.MapGet("/batches/{id:guid}/files/{fileId:guid}/download", async (Guid id, Guid fileId, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var file = await service.GetStoredFileAsync(context, id, fileId, cancellationToken);
            return file is null ? Results.NotFound(new { message = "Supplier invoice import file was not found." }) : Results.File(file.Value.Path, file.Value.ContentType, file.Value.DownloadName);
        });

        group.MapGet("/batches/{id:guid}/proof", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var file = await service.GetOriginalFileAsync(context, id, cancellationToken);
            return file is null ? Results.NotFound(new { message = "Supplier invoice proof file was not found." }) : Results.File(file.Value.Path, file.Value.ContentType, file.Value.DownloadName);
        });

        group.MapGet("/batches/{id:guid}/extracted-text", async (Guid id, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var text = await service.GetExtractedTextAsync(context, id, cancellationToken);
            return text is null ? Results.NotFound(new { message = "Extracted supplier invoice text was not found." }) : Results.Text(text.Value.Text, "text/plain");
        });

        group.MapGet("/purchase-invoices/{purchaseInvoiceId:guid}/proof", async (Guid purchaseInvoiceId, PurchaseInvoiceImportService service, HttpContext context, CancellationToken cancellationToken) =>
        {
            var file = await service.GetOriginalFileForPurchaseInvoiceAsync(context, purchaseInvoiceId, cancellationToken);
            return file is null ? Results.NotFound(new { message = "No supplier invoice proof is linked with this purchase invoice." }) : Results.File(file.Value.Path, file.Value.ContentType, file.Value.DownloadName);
        });

        return group;
    }
}

public sealed record PurchaseInvoiceImportRejectRequest(string? Reason);
