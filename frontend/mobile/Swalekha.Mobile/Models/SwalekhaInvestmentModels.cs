namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaInvestmentEndpoints.cs exactly.
// Both FD and RD optionally link to a SwalekhaAccount; MarkMatured/RecordInstallment post
// real ledger transactions against that account on the backend.

public sealed record SwalekhaFixedDepositDto(
    Guid Id,
    string BankName,
    string? FdNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    bool AutoRenew,
    decimal? TdsDeducted,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaFixedDepositPayload(
    string BankName,
    string? FdNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    bool AutoRenew,
    decimal? TdsDeducted,
    string? Notes);

public sealed record SwalekhaMarkMaturedPayload(decimal MaturityAmount, DateTime MaturityCreditedDate, string? Narration);

public sealed record SwalekhaRecurringDepositDto(
    Guid Id,
    string BankName,
    string? RdNumber,
    Guid? AccountId,
    decimal MonthlyInstallment,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    int InstallmentsPaid,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaRecurringDepositPayload(
    string BankName,
    string? RdNumber,
    Guid? AccountId,
    decimal MonthlyInstallment,
    decimal InterestRatePercent,
    int TenureMonths,
    DateTime StartDate,
    DateTime MaturityDate,
    decimal? MaturityAmount,
    string? Notes);

public sealed record SwalekhaRecordInstallmentPayload(DateTime InstallmentDate, string? Narration);
