using Garmetix.Core.Enums;

namespace Garmetix.Api.Accounting;

public sealed record PartySaveRequest(
    Guid CompanyId,
    string Name,
    string? Address,
    string? EmailId,
    string? Phone,
    string? GSTIN,
    string? PAN,
    PartyType Category);

public sealed record BankAccountSaveRequest(
    Guid CompanyId,
    string AccountNumber,
    string AccountHolderName,
    Guid BankId,
    AccountType AccountType,
    string? Branch,
    string? IFSCode,
    DateTime OpeningDate,
    bool Active,
    DateTime? ClosingDate,
    decimal OpeningBalance,
    decimal ClosingBalance);

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
    Guid StoreGroupId,
    Guid StoreId,
    Guid BankAccountId,
    Guid LedgerId,
    Guid? PartyId,
    DateTime OnDate,
    TransactionType TransactionType,
    TransactionMode TransactionMode,
    string? Narration,
    string? Reference,
    decimal Amount,
    string? PersonName);

public sealed record BankTransactionRow(
    Guid Id,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    Guid BankAccountId,
    Guid? LedgerId,
    Guid? PartyId,
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

public sealed record LedgerStatementRow(
    Guid Id,
    Guid LedgerId,
    DateTime OnDate,
    string EntryNumber,
    string SourceType,
    string? ReferenceNumber,
    string Particulars,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    string BalanceType);

public sealed record BankStatementRow(
    Guid Id,
    DateTime OnDate,
    string Description,
    string? Reference,
    decimal Debit,
    decimal Credit,
    decimal Balance,
    bool Reconciled,
    DateTime? ReconciledAt,
    string? ReconciledBy,
    string? ReconciliationReference,
    string? ReconciliationRemarks,
    Guid? BankTransactionId);

public sealed record BankReconciliationSummary(
    Guid BankAccountId,
    string BankAccountName,
    decimal StatementBalance,
    decimal OpenDebit,
    decimal OpenCredit,
    decimal ReconciledDebit,
    decimal ReconciledCredit,
    int OpenLineCount,
    int ReconciledLineCount,
    IReadOnlyList<BankStatementRow> Lines);

public sealed record BankStatementReconcileRequest(
    Guid? BankTransactionId,
    DateTime? ReconciledAt,
    string? ReconciliationReference,
    string? Remarks);

public sealed record ChequeLifecycleRequest(
    string Status,
    DateTime? ActionDate,
    string? Remarks,
    Guid? BankTransactionId);

public sealed record GstAccountingBridgeSummary(
    Guid CompanyId,
    string ReturnPeriod,
    DateTime PeriodStart,
    DateTime PeriodEndExclusive,
    decimal OutputTax,
    decimal InputTax,
    decimal NetPayable,
    decimal CreditCarryForward,
    decimal InterestLateFee,
    bool AlreadyPosted,
    Guid? JournalEntryId,
    string? JournalEntryNumber,
    string Status,
    IReadOnlyList<GstAccountingLedgerRow> LedgerRows);

public sealed record GstAccountingLedgerRow(
    string LedgerName,
    decimal Debit,
    decimal Credit,
    decimal NetAmount,
    string Meaning);

public sealed record GstAccountingPostRequest(
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string ReturnPeriod,
    DateTime? OnDate,
    decimal OutputTax,
    decimal InputTax,
    decimal InterestLateFee,
    string? Narration,
    Guid? DraftId);

public sealed record GstAccountingPostResult(
    Guid JournalEntryId,
    string EntryNumber,
    string ReferenceNumber,
    decimal OutputTax,
    decimal InputTax,
    decimal NetPayable,
    decimal CreditCarryForward,
    decimal InterestLateFee,
    string Message);

public sealed record LedgerSyncIssue(
    string Area,
    Guid EntityId,
    string EntityName,
    Guid? LedgerId,
    string Severity,
    string Message,
    string FixAction);

public sealed record LedgerSyncSummary(
    Guid? CompanyId,
    int PartyCount,
    int BankAccountCount,
    int IssueCount,
    int RepairedCount,
    IReadOnlyList<LedgerSyncIssue> Issues);

public sealed record FinancialYearLockSaveRequest(
    Guid? Id,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string FinancialYear,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    bool LockAccounting,
    bool LockSales,
    bool LockPurchase,
    bool LockInventory,
    bool LockGst,
    string? Reason);

public sealed record FinancialYearUnlockRequest(string? Reason);

public sealed record FinancialYearLockRow(
    Guid Id,
    Guid CompanyId,
    Guid? StoreGroupId,
    Guid? StoreId,
    string FinancialYear,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    bool Active,
    bool LockAccounting,
    bool LockSales,
    bool LockPurchase,
    bool LockInventory,
    bool LockGst,
    DateTime? LockedAt,
    string? LockedBy,
    string? LockReason,
    DateTime? UnlockedAt,
    string? UnlockedBy,
    string? UnlockReason);

public sealed record JournalValidationIssue(
    Guid JournalEntryId,
    string EntryNumber,
    DateTime OnDate,
    string SourceType,
    string? ReferenceNumber,
    decimal Debit,
    decimal Credit,
    decimal Difference,
    string Severity,
    string Message);

public sealed record JournalValidationSummary(
    Guid? CompanyId,
    DateTime? From,
    DateTime? To,
    int CheckedEntries,
    int IssueCount,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Difference,
    IReadOnlyList<JournalValidationIssue> Issues);

