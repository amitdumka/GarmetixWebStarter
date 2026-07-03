namespace Garmetix.Api.Backup;

public sealed class GoogleDriveBackupOptions
{
    public bool Enabled { get; set; }
    public bool UploadOnBackup { get; set; } = true;
    public string ServiceAccountJsonPath { get; set; } = string.Empty;
    public string ServiceAccountJson { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public int RetentionCount { get; set; } = 30;
    public string ApplicationName { get; set; } = "Garmetix Backup";
}
