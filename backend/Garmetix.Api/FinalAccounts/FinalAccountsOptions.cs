namespace Garmetix.Api.FinalAccounts;

public sealed class FinalAccountsOptions
{
    public bool DefaultEnabled { get; set; }
    public string PostingMode { get; set; } = FinalAccountsSettingsDefaults.PostingMode;
    public string StatementTemplate { get; set; } = FinalAccountsSettingsDefaults.StatementTemplate;
    public string InventoryValuationMethod { get; set; } = FinalAccountsSettingsDefaults.InventoryValuationMethod;
    public int RoundingScale { get; set; } = FinalAccountsSettingsDefaults.RoundingScale;
}
