using Garmetix.Api.Auth;
using Garmetix.Core.Models.Swalekha;
using Garmetix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Garmetix.Api.Swalekha;

public sealed record SwalekhaDocumentDto(
    Guid Id,
    string EntityType,
    Guid? EntityId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? Notes,
    DateTime CreatedAt);

/// <summary>
/// PersonalFin_15 - Document Vault. Scanned proofs (FD/RD certificates, loan agreements,
/// insurance policy PDFs) attached to a Swalekha record, or standalone. Follows the same local-
/// disk-storage pattern PurchaseInvoiceImportService already established for this codebase:
/// a configurable storage root, per-owner subfolders, extension whitelist, and Results.File for
/// download - just scoped Owner-only instead of Company-scoped.
/// </summary>
public static class SwalekhaDocumentEndpoints
{
    private const long MaxUploadBytes = 15L * 1024L * 1024L;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp"
    };

    public static RouteGroupBuilder MapSwalekhaDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/swalekha/documents")
            .WithTags("Swalekha Documents")
            .RequireAuthorization(GarmetixPolicies.SwalekhaOwner);

        group.MapGet("/", ListDocumentsAsync);
        group.MapPost("/", UploadDocumentAsync).DisableAntiforgery();
        group.MapGet("/{id:guid}/download", DownloadDocumentAsync);
        group.MapDelete("/{id:guid}", DeleteDocumentAsync);

        return group;
    }

    private static async Task<IResult> ListDocumentsAsync(SwalekhaDbContext db, string? entityType, Guid? entityId, CancellationToken cancellationToken)
    {
        var query = db.SwalekhaDocuments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(d => d.EntityType == entityType);
        }
        if (entityId.HasValue)
        {
            query = query.Where(d => d.EntityId == entityId.Value);
        }

        var documents = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return Results.Ok(documents.Select(ToDto).ToList());
    }

    private static async Task<IResult> UploadDocumentAsync(
        HttpRequest request,
        SwalekhaDbContext db,
        SwalekhaOwnerContext ownerContext,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Upload must be multipart/form-data." });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return Results.BadRequest(new { message = "A file is required." });
        }

        if (file.Length <= 0)
        {
            return Results.BadRequest(new { message = "The uploaded file is empty." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.BadRequest(new { message = "File is too large. Maximum allowed size is 15 MB." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return Results.BadRequest(new { message = "Only PDF, PNG, JPG, JPEG and WEBP files are supported." });
        }

        var entityType = form["entityType"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Results.BadRequest(new { message = "entityType is required (e.g. FixedDeposit, Loan, InsurancePolicy, General)." });
        }

        Guid? entityId = null;
        if (Guid.TryParse(form["entityId"].FirstOrDefault(), out var parsedEntityId))
        {
            entityId = parsedEntityId;
        }

        var storageRoot = configuration["Swalekha:DocumentVault:StoragePath"];
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            storageRoot = Path.Combine(environment.ContentRootPath, "data", "swalekha-documents");
        }

        var documentId = Guid.NewGuid();
        var storageDir = Path.Combine(storageRoot, ownerContext.OwnerId.ToString("N"));
        Directory.CreateDirectory(storageDir);

        var storedFilePath = Path.Combine(storageDir, $"{documentId:N}{extension}");
        await using (var output = File.Create(storedFilePath))
        {
            await file.CopyToAsync(output, cancellationToken);
        }

        var document = new SwalekhaDocument
        {
            Id = documentId,
            EntityType = entityType.Trim(),
            EntityId = entityId,
            FileName = Path.GetFileName(file.FileName),
            StoredFilePath = storedFilePath,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            FileSizeBytes = file.Length,
            Notes = form["notes"].FirstOrDefault()
        };

        db.SwalekhaDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/swalekha/documents/{document.Id}", ToDto(document));
    }

    private static async Task<IResult> DownloadDocumentAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var document = await db.SwalekhaDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null || !File.Exists(document.StoredFilePath))
        {
            return Results.NotFound(new { message = "Document not found." });
        }

        return Results.File(document.StoredFilePath, document.ContentType, document.FileName);
    }

    private static async Task<IResult> DeleteDocumentAsync(Guid id, SwalekhaDbContext db, CancellationToken cancellationToken)
    {
        var document = await db.SwalekhaDocuments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null) return Results.NotFound();

        document.Deleted = true;
        document.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static SwalekhaDocumentDto ToDto(SwalekhaDocument document) => new(
        document.Id,
        document.EntityType,
        document.EntityId,
        document.FileName,
        document.ContentType,
        document.FileSizeBytes,
        document.Notes,
        document.CreatedAt);
}
