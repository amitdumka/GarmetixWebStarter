namespace Garmetix.Api.Licensing;

public sealed record SaveSaaSClientRequest(
    string ClientCode,
    string Name,
    string? Email,
    string? Mobile,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? ZipCode,
    string? Gstin,
    bool Active = true);

public sealed record SaveSaaSPlanRequest(
    string PlanName,
    int MaxCompanies,
    int MaxStoreGroups,
    int MaxStores,
    int MaxUsers,
    string? IncludedModulesCsv,
    bool Active = true);

public sealed record GenerateSaaSTokenRequest(
    Guid SaaSClientId,
    Guid SaaSPlanId,
    int? ValidityDays,
    string? Notes);

public sealed record ActivateSaaSTokenRequest(string TokenString, Guid CompanyId);

public sealed record SaaSTokenDto(
    Guid Id,
    string TokenString,
    Guid SaaSClientId,
    string ClientName,
    Guid SaaSPlanId,
    string PlanName,
    int ValidityDays,
    DateTime ExpiresAt,
    bool IsActivated,
    DateTime? ActivatedAtUtc,
    Guid? ActivatedCompanyId,
    string? ActivatedCompanyName,
    string? Notes);

public sealed record TenantSubscriptionDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    Guid SaaSClientId,
    string ClientName,
    Guid SaaSPlanId,
    string PlanName,
    int MaxCompanies,
    int MaxStoreGroups,
    int MaxStores,
    int MaxUsers,
    int CurrentStoreGroups,
    int CurrentStores,
    int CurrentUsers,
    DateTime ValidFrom,
    DateTime ValidTo,
    bool IsActive);

public sealed record ActivateSaaSTokenResponse(string Message, TenantSubscriptionDto Subscription);

public sealed record SaaSClientCompanyDto(Guid CompanyId, string CompanyName, string CompanyCode, bool Linked, bool LinkedToAnotherClient);
