namespace Garmetix.Api.DayBook;

public sealed record DayBookRowDto(
    Guid Id,
    string DocumentType,
    string DocumentSubType,
    string DocumentNumber,
    DateTime OnDate,
    string PartyName,
    string Particulars,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal NetAmount,
    string PaymentMode,
    string Status,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string SourcePath,
    string DetailApiPath,
    string OpenActionLabel);

public sealed record DayBookSummaryDto(
    int Count,
    decimal DebitAmount,
    decimal CreditAmount,
    decimal NetAmount,
    int SaleCount,
    int PurchaseCount,
    int VoucherCount,
    int PaymentCount,
    int JournalCount);

public sealed record DayBookResultDto(
    IReadOnlyList<DayBookRowDto> Rows,
    DayBookSummaryDto Summary,
    int Page,
    int PageSize,
    int Total,
    DateTime From,
    DateTime To);

public sealed record DayBookDetailDto(
    DayBookRowDto Row,
    object? Detail,
    IReadOnlyList<object> Lines,
    string SourcePath,
    string OpenActionLabel);
