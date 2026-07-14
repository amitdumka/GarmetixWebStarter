namespace Garmetix.Api.DotMatrix;

public sealed class DotMatrixPrintingOptions
{
    public bool Enabled { get; set; } = false;
    public bool RunWorker { get; set; } = false;
    public string PrinterName { get; set; } = "EPSON_LX810";
    public string OutputMode { get; set; } = "BridgeService"; // BridgeService, Disabled, SpoolFile, LpCommand
    public string SpoolDirectory { get; set; } = "/app/data/dotmatrix-spool";
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public int LineWidth { get; set; } = 136;
    public int PollSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int RetryLimit { get; set; } = 10;
    public string LpCommand { get; set; } = "lp";
    public string LpArgumentsTemplate { get; set; } = "-d {printer} -o raw";
}
