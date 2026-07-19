namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaMutualFundEndpoints.cs exactly. XIRR is
// computed server-side (SwalekhaXirrCalculator) - the mobile client just reads the result
// from GET /{id}/returns, no calculator ported here.

public sealed record SwalekhaMutualFundDto(
    Guid Id,
    string SchemeName,
    string? Amc,
    string? FolioNumber,
    Guid? AccountId,
    string InvestmentMode,
    decimal? SipAmount,
    int? SipDayOfMonth,
    DateTime? LastSipInstallmentDate,
    bool SipDueThisMonth,
    decimal? CurrentNav,
    DateTime? CurrentNavUpdatedAt,
    decimal CurrentUnits,
    decimal TotalInvested,
    decimal? CurrentValue,
    decimal? AbsoluteReturn,
    decimal? ReturnPercent,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaMutualFundPayload(
    string SchemeName,
    string? Amc,
    string? FolioNumber,
    Guid? AccountId,
    string InvestmentMode,
    decimal? SipAmount,
    int? SipDayOfMonth,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaUpdateNavPayload(decimal CurrentNav);

public sealed record SwalekhaMutualFundTransactionDto(
    Guid Id,
    Guid FundId,
    string TransactionType,
    DateTime TransactionDate,
    decimal Units,
    decimal NavAtTransaction,
    decimal Amount,
    string Narration,
    DateTime CreatedAt);

public sealed record SwalekhaMutualFundTransactionPayload(
    string TransactionType,
    DateTime TransactionDate,
    decimal? Amount,
    decimal? Units,
    decimal NavAtTransaction,
    string? Narration);

public sealed record SwalekhaMutualFundTransactionList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaMutualFundTransactionDto> Rows);

public sealed record SwalekhaMutualFundReturnsDto(
    decimal CurrentUnits,
    decimal? CurrentNav,
    decimal? CurrentValue,
    decimal TotalInvested,
    decimal? AbsoluteReturn,
    decimal? ReturnPercent,
    decimal? Xirr,
    bool XirrAvailable,
    string? XirrNote);
