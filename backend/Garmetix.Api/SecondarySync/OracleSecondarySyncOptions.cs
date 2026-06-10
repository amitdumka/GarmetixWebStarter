namespace Garmetix.Api.SecondarySync;

public sealed class OracleSecondarySyncOptions
{
    public bool Enabled { get; set; } = false;
    public bool RunOnStartup { get; set; } = false;
    public int IntervalSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 250;
    public string ConnectionString { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string TenantId { get; set; } = "garmetix-default";
    public string SourceApplication { get; set; } = "GarmetixWeb";
    public string Direction { get; set; } = "PushToOracle";
    public string ConflictPolicy { get; set; } = "ManualReview";
    public bool CreateOracleSchema { get; set; } = true;
    public bool PushDeletedRows { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 60;
    public string WalletDirectory { get; set; } = string.Empty;
    public string TnsAdmin { get; set; } = string.Empty;
    public bool PullExternalEvents { get; set; } = true;
    public bool ApplyInboundAutomatically { get; set; } = false;
    public string[] EntityNames { get; set; } = [];
    public string EntityNamesCsv { get; set; } = string.Empty;
    public string[] AutoApplyEntityNames { get; set; } = [];
    public string AutoApplyEntityNamesCsv { get; set; } = string.Empty;
    public string[] TrustedSourceApplications { get; set; } = [];
    public string TrustedSourceApplicationsCsv { get; set; } = string.Empty;
    public bool RequireTrustedSourceForAutoApply { get; set; } = true;
    public int AutoApplyBatchSize { get; set; } = 50;
}
