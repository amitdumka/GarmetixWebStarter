namespace Swalekha.Mobile.Models;

// Mirrors backend/Garmetix.Api/Swalekha/SwalekhaShareEndpoints.cs and
// SwalekhaOtherAssetEndpoints.cs exactly.

public sealed record SwalekhaShareHoldingDto(
    Guid Id,
    string Symbol,
    string? CompanyName,
    string? Exchange,
    string? DematAccount,
    string? Broker,
    Guid? AccountId,
    decimal? CurrentPrice,
    DateTime? CurrentPriceUpdatedAt,
    decimal CurrentQuantity,
    decimal TotalInvested,
    decimal RealizedPnL,
    decimal? CurrentValue,
    decimal? UnrealizedPnL,
    decimal? UnrealizedPnLPercent,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaShareHoldingPayload(
    string Symbol,
    string? CompanyName,
    string? Exchange,
    string? DematAccount,
    string? Broker,
    Guid? AccountId,
    bool IsActive,
    string? Notes);

public sealed record SwalekhaUpdateSharePricePayload(decimal CurrentPrice);

public sealed record SwalekhaShareTransactionDto(
    Guid Id,
    Guid HoldingId,
    string TransactionType,
    DateTime TransactionDate,
    decimal Quantity,
    decimal PricePerShare,
    decimal Amount,
    string Narration,
    decimal? RealizedPnLOnSale,
    DateTime CreatedAt);

public sealed record SwalekhaShareTransactionPayload(
    string TransactionType,
    DateTime TransactionDate,
    decimal Quantity,
    decimal PricePerShare,
    string? Narration);

public sealed record SwalekhaShareTransactionList(int Page, int PageSize, int TotalCount, IReadOnlyList<SwalekhaShareTransactionDto> Rows);

public sealed record SwalekhaOtherAssetDto(
    Guid Id,
    string AssetType,
    string Name,
    decimal CurrentValue,
    DateTime AsOfDate,
    bool IsActive,
    string? Notes,
    DateTime CreatedAt);

public sealed record SwalekhaOtherAssetPayload(
    string AssetType,
    string Name,
    decimal CurrentValue,
    DateTime AsOfDate,
    bool IsActive,
    string? Notes);
