namespace Garmetix.Api.Automation;

public sealed class PayrollAutomationOptions
{
    public bool Enabled { get; set; } = true;
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public int RunHour { get; set; } = 2;
    public int RunMinute { get; set; }
    public bool RunOnStartup { get; set; } = true;
}
