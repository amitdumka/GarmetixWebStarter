namespace Garmetix.Api.Communication;

/// <summary>Bound from configuration section "Communication:EmailQueue". IOptionsMonitor-backed (like OracleSecondarySyncOptions) so Enabled/interval can flip live without a restart.</summary>
public sealed class EmailQueueOptions
{
    public bool Enabled { get; set; } = true;
    public bool RunOnStartup { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int LeaseDurationSeconds { get; set; } = 120;
    public int DefaultMaxAttempts { get; set; } = 8;
    public int BaseBackoffSeconds { get; set; } = 30;
    public int MaxBackoffSeconds { get; set; } = 3600;
    public double JitterRatio { get; set; } = 0.2;
}
