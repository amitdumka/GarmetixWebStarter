using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Swalekha;

/// <summary>
/// A scanned document (PersonalFin_15 - Document Vault) attached to a Swalekha record - FD/RD
/// certificates, loan agreements, insurance policy PDFs, or a general standalone upload.
/// EntityType/EntityId are plain fields, not a formal FK (matching every other Swalekha table's
/// FK-attribute-free convention) - EntityId is nullable so a document can exist unattached
/// (EntityType "General") before being filed against a specific record, or simply as a scan kept
/// on its own. The actual file lives on disk under the configured storage root; this row is the
/// metadata/index pointing at it.
/// </summary>
public class SwalekhaDocument : SwalekhaOwnedEntity
{
    public SwalekhaDocument()
    {
        EntityType = string.Empty;
        FileName = string.Empty;
        StoredFilePath = string.Empty;
        ContentType = string.Empty;
    }

    [Display(Name = "Entity Type")] public string EntityType { get; set; }
    [Display(Name = "Entity Id")] public Guid? EntityId { get; set; }
    [Display(Name = "File Name")] public string FileName { get; set; }
    [Display(Name = "Stored File Path")] public string StoredFilePath { get; set; }
    [Display(Name = "Content Type")] public string ContentType { get; set; }
    [Display(Name = "File Size Bytes")] public long FileSizeBytes { get; set; }
    [Display(Name = "Notes")] public string? Notes { get; set; }
}
