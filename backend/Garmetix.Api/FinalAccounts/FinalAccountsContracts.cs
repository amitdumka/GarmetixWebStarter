namespace Garmetix.Api.FinalAccounts;

public static class FinalAccountsSettingsDefaults
{
    public const string FeatureKey = "FINAL_ACCOUNTS";
    public const string ApiRoot = "/api/final-accounts";
    public const string RouteRoot = "/final-accounts";
    public const string PostingMode = "ManualSync";
    public const string StatementTemplate = "GarmentRetail.v1";
    public const string InventoryValuationMethod = "WeightedAverage";
    public const int RoundingScale = 2;
}

public sealed record FinalAccountsScopeDto(Guid? CompanyId, Guid? StoreGroupId, Guid? StoreId);

public sealed record FinalAccountsSettingsResponse(
    bool Enabled,
    string FeatureKey,
    string ApiRoot,
    string RouteRoot,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string PostingMode,
    string StatementTemplate,
    string InventoryValuationMethod,
    int RoundingScale,
    bool AllowHistoricalBackfill,
    bool AllowTallyExport,
    bool AllowProjections,
    bool AllowPeriodReopen,
    DateTimeOffset CheckedAtUtc,
    string Message);

public sealed record FinalAccountsSaveSettingsRequest(
    bool? Enabled,
    Guid? CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string? PostingMode,
    string? StatementTemplate,
    string? InventoryValuationMethod,
    int? RoundingScale,
    bool? AllowHistoricalBackfill,
    bool? AllowTallyExport,
    bool? AllowProjections,
    bool? AllowPeriodReopen);
