using Garmetix.Core.Enums;

namespace Garmetix.Api.Commercial;

public sealed record CommercialNoteRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    NoteType NoteType,
    PartyType PartyType,
    Guid? PartyId,
    Guid? CustomerId,
    Guid? VendorId,
    string PartyName,
    string? PartyGstin,
    string? Reason,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal Amount,
    string? SourceType,
    Guid? SourceId,
    string? SourceNumber,
    string? Remarks);

public sealed record CommercialNoteRow(
    Guid Id,
    string NoteNumber,
    DateTime OnDate,
    string NoteType,
    string PartyType,
    string PartyName,
    string? PartyGstin,
    string SourceType,
    string? SourceNumber,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal Amount,
    bool IsAdjusted,
    decimal AdjustedAmount,
    string? Reason);

public sealed record CustomerAdvanceReceiptRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    Guid? CustomerId,
    string CustomerName,
    string? CustomerMobileNumber,
    decimal Amount,
    PaymentMode PaymentMode,
    Guid? BankAccountId,
    string? ReferenceNumber,
    string? Remarks);

public sealed record CustomerAdvanceReceiptRow(
    Guid Id,
    string ReceiptNumber,
    DateTime OnDate,
    Guid CustomerId,
    string CustomerName,
    string? CustomerMobileNumber,
    decimal Amount,
    decimal AdjustedAmount,
    decimal AvailableAmount,
    string PaymentMode,
    string? ReferenceNumber);

public sealed record LoyaltyProgramRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    bool Enabled,
    string Name,
    decimal EarnPointsPerRupee,
    decimal RedeemValuePerPoint,
    decimal MinimumBillAmount,
    int? ExpiryDays);

public sealed record LoyaltyProgramDto(
    Guid Id,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    bool Enabled,
    string Name,
    decimal EarnPointsPerRupee,
    decimal RedeemValuePerPoint,
    decimal MinimumBillAmount,
    int? ExpiryDays);

public sealed record LoyaltyLedgerRow(
    Guid Id,
    DateTime OnDate,
    Guid CustomerId,
    string CustomerName,
    string SourceType,
    string? SourceNumber,
    decimal PointsIn,
    decimal PointsOut,
    decimal BalanceAfter,
    string? Remarks);


public sealed record LoyaltyAdjustmentRequest(
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId,
    decimal PointsIn,
    decimal PointsOut,
    string? Remarks);

public sealed record CustomerLoyaltySummary(
    Guid CustomerId,
    string CustomerName,
    decimal LoyaltyPoints,
    decimal CreditBalance,
    decimal LifetimeBillAmount,
    int BillCount);
