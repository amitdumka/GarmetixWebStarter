using System.Security.Cryptography;
using Garmetix.Core.Models.Communication;
using Garmetix.Infrastructure.Data;

namespace Garmetix.Api.Communication;

/// <summary>
/// Stores internal-message attachments on local disk following the PurchaseInvoiceImportService/
/// SwalekhaDocumentEndpoints pattern: configurable storage root, per-company/date subfolders,
/// random non-guessable stored file name, checksum, allow-listed extension, size cap.
/// </summary>
public sealed class CommunicationAttachmentStorageService(IConfiguration configuration, IWebHostEnvironment environment)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt"
    };

    private const long MaxSizeBytes = 15 * 1024 * 1024; // 15 MB, matches SwalekhaDocument's cap

    public string StorageRoot()
    {
        var configured = configuration["Communication:AttachmentStorage:StoragePath"];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "data", "communication-attachments")
            : configured;
    }

    public (bool IsValid, string? Error) ValidateUpload(string originalFileName, long sizeBytes)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return (false, $"File type '{extension}' is not allowed.");
        }

        if (sizeBytes <= 0 || sizeBytes > MaxSizeBytes)
        {
            return (false, $"File size must be between 1 byte and {MaxSizeBytes / (1024 * 1024)} MB.");
        }

        return (true, null);
    }

    public async Task<(string StoredFileName, string StoredRelativePath, string Sha256Checksum)> SaveAsync(
        Guid? companyId, Guid ownerId, Stream content, string originalFileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativeDir = Path.Combine(
            companyId?.ToString("N") ?? "global",
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            ownerId.ToString("N"));
        var absoluteDir = Path.Combine(StorageRoot(), relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var absolutePath = Path.Combine(absoluteDir, storedFileName);
        using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        string checksum;
        using (var sha256 = SHA256.Create())
        using (var verifyStream = File.OpenRead(absolutePath))
        {
            var hash = await sha256.ComputeHashAsync(verifyStream, cancellationToken);
            checksum = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var storedRelativePath = Path.Combine(relativeDir, storedFileName);
        return (storedFileName, storedRelativePath, checksum);
    }

    public string ResolveAbsolutePath(string storedRelativePath) => Path.Combine(StorageRoot(), storedRelativePath);
}
