using Garmetix.Core.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace Garmetix.Core.Models.Printing;

public class DotMatrixPrintSetting : StoreBase
{
    [MaxLength(120)] public string PrinterName { get; set; } = "EPSON_LX810";
    [MaxLength(40)] public string OutputMode { get; set; } = "BridgeService"; // BridgeService, Disabled, SpoolFile, LpCommand
    [MaxLength(500)] public string SpoolDirectory { get; set; } = "/app/data/dotmatrix-spool";
    [MaxLength(80)] public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public bool Enabled { get; set; } = false;
    public bool PrintTransactions { get; set; } = true;
    public bool PrintDayOpeningClosing { get; set; } = true;
    public bool PrintEditsAndDeletes { get; set; } = true;
    public bool PrintAttendanceInDaySummary { get; set; } = true;
    public bool PrintBankUpiSummary { get; set; } = true;
    public int LineWidth { get; set; } = 136;
    public int RetryLimit { get; set; } = 10;
    public int PollSeconds { get; set; } = 5;
    [MaxLength(300)] public string? Remarks { get; set; }
}

public class DotMatrixPrintQueueEntry : StoreBase
{
    public DateTime BusinessDate { get; set; } = DateTime.Today;
    public DateTime OperationTimeUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(40)] public string EventType { get; set; } = "Transaction";
    [MaxLength(40)] public string ActionType { get; set; } = "Create";
    [MaxLength(80)] public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    [MaxLength(120)] public string SourceNumber { get; set; } = string.Empty;
    [MaxLength(120)] public string PartyName { get; set; } = string.Empty;
    [MaxLength(120)] public string PaymentMode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long SequenceNo { get; set; }
    public int LineWidth { get; set; } = 136;
    [MaxLength(120)] public string PrinterName { get; set; } = "EPSON_LX810";
    [MaxLength(40)] public string Status { get; set; } = "Pending"; // Pending, Printing, Printed, Failed, Skipped
    public int RetryCount { get; set; }
    public DateTime? PrintedAtUtc { get; set; }
    [MaxLength(500)] public string? ErrorMessage { get; set; }
    [MaxLength(240)] public string DeduplicationKey { get; set; } = string.Empty;
    public string PrintableText { get; set; } = string.Empty;
}
