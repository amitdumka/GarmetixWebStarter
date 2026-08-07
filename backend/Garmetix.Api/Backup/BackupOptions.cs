namespace Garmetix.Api.Backup;

public sealed class BackupOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Relative paths resolve against AppContext.BaseDirectory at startup (see DatabaseBackupService's
    /// ResolveDirectory) rather than being used as-is - the old "/app/backups" default was a Docker
    /// WORKDIR convention that doesn't exist on this app's actual bare-metal/systemd SRP deployment,
    /// which caused every backup-listing/status/maintenance call to throw UnauthorizedAccessException
    /// there. An absolute path here is still honored unchanged, so an intentionally-configured Docker
    /// or custom deployment keeps working exactly as before.
    /// </summary>
    public string Directory { get; set; } = "backups";
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public int RunHour { get; set; } = 2;
    public int RunMinute { get; set; } = 30;
    public bool RunOnStartup { get; set; }
    public int RetentionCount { get; set; } = 14;
    public int RetentionDays { get; set; } = 30;
    public int KeepMinimum { get; set; } = 10;
    public string RestoreDrillMarkerPath { get; set; } = string.Empty;
    public long MaxRestoreBytes { get; set; } = 1_073_741_824;
}
