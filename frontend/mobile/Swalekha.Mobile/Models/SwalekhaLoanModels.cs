namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaLoanEndpoints.cs exactly. EMI calculation and
// the amortization schedule are both computed server-side (SwalekhaLoanCalculator) - not
// reimplemented on the client.

public sealed record SwalekhaLoanDto(
    Guid Id,
    string LoanType,
    string LenderName,
    string? LoanNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    decimal EmiAmount,
    DateTime StartDate,
    decimal OutstandingPrincipal,
    bool IsClosed,
    DateTime? ClosedAt,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaLoanPayload(
    string LoanType,
    string LenderName,
    string? LoanNumber,
    Guid? AccountId,
    decimal PrincipalAmount,
    decimal InterestRatePercent,
    int TenureMonths,
    decimal EmiAmount,
    DateTime StartDate,
    string? Notes);

public sealed record SwalekhaCalculateEmiPayload(decimal PrincipalAmount, decimal InterestRatePercent, int TenureMonths);

public sealed record SwalekhaCalculateEmiResult(decimal EmiAmount);

public sealed record SwalekhaLoanPaymentDto(
    Guid Id,
    Guid LoanId,
    string PaymentType,
    DateTime PaymentDate,
    decimal Amount,
    decimal PrincipalComponent,
    decimal InterestComponent,
    decimal OutstandingAfter,
    string Narration,
    DateTime CreatedAt);

public sealed record SwalekhaLoanPaymentPayload(
    string PaymentType,
    DateTime PaymentDate,
    decimal Amount,
    string? Narration);

public sealed record SwalekhaLoanPaymentList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaLoanPaymentDto> Rows);

public sealed record SwalekhaAmortizationRow(
    int MonthNumber,
    decimal OpeningBalance,
    decimal InterestComponent,
    decimal PrincipalComponent,
    decimal ClosingBalance);
