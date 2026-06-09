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
    public bool CreateOracleSchema { get; set; } = true;
    public bool PushDeletedRows { get; set; } = true;
    public int CommandTimeoutSeconds { get; set; } = 60;
    public string[] EntityNames { get; set; } = [];
    public string EntityNamesCsv { get; set; } = string.Empty;
}
