using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Backup;

/// <summary>
/// One Company's own linked Google Drive account for off-site backups - the per-tenant replacement
/// for the older single, server-wide service-account config (GoogleDriveBackupOptions/GoogleDriveBackupService),
/// which is left untouched for legacy compatibility. Each Company connects its own Drive via OAuth
/// (web-based "Connect Google Drive" consent flow), so backups genuinely land in that company's own
/// account/folder instead of one shared Drive for every company on the platform.
/// </summary>
public class CompanyGoogleDriveConnection : BaseEntity
{
    public CompanyGoogleDriveConnection()
    {
        GoogleAccountEmail = string.Empty;
        EncryptedRefreshToken = string.Empty;
        FolderId = string.Empty;
        FolderName = string.Empty;
    }

    [Display(Name = "Company Id")] public Guid CompanyId { get; set; }
    [Display(Name = "Google Account Email")] public string GoogleAccountEmail { get; set; }
    [Display(Name = "Encrypted Refresh Token")] public string EncryptedRefreshToken { get; set; }
    [Display(Name = "Folder Id")] public string FolderId { get; set; }
    [Display(Name = "Folder Name")] public string FolderName { get; set; }
    [Display(Name = "Connected At")] public DateTime ConnectedAtUtc { get; set; }
    [Display(Name = "Connected By")] public string? ConnectedByUserName { get; set; }
    [Display(Name = "Is Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Last Success At")] public DateTime? LastSuccessAtUtc { get; set; }
    [Display(Name = "Last Action")] public string? LastAction { get; set; }
    [Display(Name = "Last Error")] public string? LastError { get; set; }
}
