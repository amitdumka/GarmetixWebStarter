using Garmetix.Core.Enums;

namespace Garmetix.Api.Accounting;

public sealed record VoucherSaveRequest(
    Guid? Id,
    string VoucherNumber,
    DateTime OnDate,
    VoucherType VoucherType,
    string PartyName,
    string Particulars,
    decimal Amount,
    string? Remarks,
    string? SlipNumber,
    PaymentMode PaymentMode,
    string? PaymentDetails,
    bool IsParty,
    Guid? PartyId,
    Guid LedgerId,
    Guid EmployeeId,
    Guid? AccountNumber,
    Guid CompanyId,
    Guid StoreGroupId,
    Guid StoreId);

public sealed record AccountingPostResult(
    Guid VoucherId,
    Guid JournalEntryId,
    Guid DebitLedgerId,
    Guid CreditLedgerId,
    Guid? BankTransactionId,
    Guid? ChequeLogId);

public sealed record BankTransactionSaveRequest(
    Guid? Id,
    Guid CompanyId,
    Guid BankAccountId,
    DateTime OnDate,
    TransactionType TransactionType,
    TransactionMode TransactionMode,
    string? Narration,
    string? Reference,
    decimal Amount,
    string? PersonName);

public sealed record TrialBalanceRow(
    Guid LedgerId,
    string LedgerName,
    string LedgerGroup,
    decimal Debit,
    decimal Credit,
    decimal ClosingDebit,
    decimal ClosingCredit);

public sealed record BankStatementRow(
    Guid Id,
    DateTime OnDate,
    string Description,
    string? Reference,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    bool Reconciled);
