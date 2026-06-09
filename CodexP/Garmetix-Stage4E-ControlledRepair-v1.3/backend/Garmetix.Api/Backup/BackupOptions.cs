namespace Garmetix.Api.Backup;

public sealed class BackupOptions
{
    public bool Enabled { get; set; } = true;
    public string Directory { get; set; } = "/app/backups";
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public int RunHour { get; set; } = 2;
    public int RunMinute { get; set; } = 30;
    public bool RunOnStartup { get; set; }
    public int RetentionCount { get; set; } = 14;
    public long MaxRestoreBytes { get; set; } = 1_073_741_824;
}
