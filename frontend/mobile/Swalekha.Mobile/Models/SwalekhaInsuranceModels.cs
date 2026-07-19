namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaInsuranceEndpoints.cs exactly.
// Reuses SwalekhaMarkMaturedPayload from SwalekhaInvestmentModels.cs - same shape, same action.

public sealed record SwalekhaInsurancePolicyDto(
    Guid Id,
    string PolicyType,
    string Insurer,
    string? PolicyNumber,
    Guid? AccountId,
    decimal? SumAssured,
    decimal PremiumAmount,
    string PremiumFrequency,
    DateTime StartDate,
    string? NomineeName,
    string? NomineeRelationship,
    DateTime? MaturityDate,
    decimal? MaturityAmount,
    DateTime? LastPremiumPaidDate,
    DateTime NextPremiumDueDate,
    bool PremiumDueNow,
    bool IsMatured,
    DateTime? MaturedAt,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaInsurancePolicyPayload(
    string PolicyType,
    string Insurer,
    string? PolicyNumber,
    Guid? AccountId,
    decimal? SumAssured,
    decimal PremiumAmount,
    string PremiumFrequency,
    DateTime StartDate,
    string? NomineeName,
    string? NomineeRelationship,
    DateTime? MaturityDate,
    decimal? MaturityAmount,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaPayPremiumPayload(DateTime PaymentDate, string? Narration);
