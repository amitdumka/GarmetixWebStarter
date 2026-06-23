using Garmetix.Core.Enums;

namespace Garmetix.Api.Onboarding;

public sealed record EnumOptionDto(string Label, int Value, string Name);

public sealed record ClientOnboardingOptionsDto(
    IReadOnlyList<EnumOptionDto> CompanyTypes,
    IReadOnlyList<EnumOptionDto> StoreCategories,
    IReadOnlyList<EnumOptionDto> AppOperations,
    IReadOnlyList<EnumOptionDto> Genders,
    ClientOnboardingSummaryDto Summary,
    IReadOnlyList<string> FlowSteps,
    IReadOnlyList<string> ModelMappingNotes);

public sealed record ClientOnboardingSummaryDto(
    int CompanyCount,
    int StoreGroupCount,
    int StoreCount,
    bool HasCompany,
    string? FirstCompanyName,
    string? FirstCompanyCode);

public sealed record ClientOnboardingRequest(
    OnboardingClientInfoDto ClientDetails,
    OnboardingCompanyInfoDto CompanyDetails,
    OnboardingAddressInfoDto AddressDetails,
    OnboardingCompanyConfigInfoDto CompanyConfig,
    OnboardingKeyPersonalInfoDto KeyPersonalDetails,
    bool SeedBasicStructure = true,
    bool IsTermsAccepted = false,
    bool IsPrivacyPolicyAccepted = false);

public sealed record OnboardingClientInfoDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string PhoneNumber,
    DateTime? DateOfBirth,
    Gender Gender);

public sealed record OnboardingCompanyInfoDto(
    string CompanyName,
    string GSTIN,
    string PAN,
    CompanyType CompanyType,
    string CompanyEmail,
    string CompanyPhoneNumber,
    DateTime? DateOfIncorporation,
    StoreCategory StoreCategory,
    string? CompanyWebsite = null,
    string? CompanyDescription = null,
    string? CompanyTagline = null,
    string? CIN = null);

public sealed record OnboardingAddressInfoDto(
    string StreetAddress,
    string City,
    string StateOrProvince,
    string PostalCode,
    string Country);

public sealed record OnboardingCompanyConfigInfoDto(
    string ClientCode,
    string GroupCode,
    string StoreCode,
    AppOperation OperationMode,
    string? GroupName = null,
    string? StoreName = null,
    string? BaseCompanyUrl = null);

public sealed record OnboardingKeyPersonalInfoDto(
    string StoreManagerName,
    string StoreManagerEmail,
    string StoreManagerPhoneNumber,
    string AccountantName,
    string AccountantEmail,
    string AccountantPhoneNumber);

public sealed record ClientOnboardingResponseDto(
    string Message,
    DateTimeOffset CompletedAtUtc,
    ClientOnboardingTargetDto Target,
    ClientOnboardingCreatedCountsDto Created,
    IReadOnlyList<string> LoginHints,
    IReadOnlyList<string> Notes);

public sealed record ClientOnboardingTargetDto(
    Guid CompanyId,
    string CompanyName,
    string CompanyCode,
    Guid StoreGroupId,
    string StoreGroupName,
    string StoreGroupCode,
    Guid StoreId,
    string StoreName,
    string StoreCode);

public sealed record ClientOnboardingCreatedCountsDto(
    int Companies,
    int StoreGroups,
    int Stores,
    int Banks,
    int Taxes,
    int Transactions,
    int LedgerGroups,
    int Ledgers,
    int BankAccounts,
    int Employees,
    int Salesmen,
    int Users,
    int ProductCategories,
    int ProductSubCategories,
    int Brands);

internal sealed class ClientOnboardingCounters
{
    public int Companies { get; set; }
    public int StoreGroups { get; set; }
    public int Stores { get; set; }
    public int Banks { get; set; }
    public int Taxes { get; set; }
    public int Transactions { get; set; }
    public int LedgerGroups { get; set; }
    public int Ledgers { get; set; }
    public int BankAccounts { get; set; }
    public int Employees { get; set; }
    public int Salesmen { get; set; }
    public int Users { get; set; }
    public int ProductCategories { get; set; }
    public int ProductSubCategories { get; set; }
    public int Brands { get; set; }

    public ClientOnboardingCreatedCountsDto ToDto() => new(
        Companies,
        StoreGroups,
        Stores,
        Banks,
        Taxes,
        Transactions,
        LedgerGroups,
        Ledgers,
        BankAccounts,
        Employees,
        Salesmen,
        Users,
        ProductCategories,
        ProductSubCategories,
        Brands);
}
