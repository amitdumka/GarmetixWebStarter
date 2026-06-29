# Stage 13D.2 Books Accounting Contracts

Version: 5.13.26
Branch: Version5

## Scope

This stage adds contract parity checks for the Books/accounting read models and request DTOs used by the modular Books app.

## Command

```powershell
npm.cmd run modular:books:accounting-contract
```

## Covered Contracts

- `VoucherSaveRequest`
- `BankTransactionRow`
- `TrialBalanceRow`
- `BankStatementRow`
- `BankReconciliationSummary`
- `LedgerSyncSummary`
- `LedgerSyncIssue`
- `FinancialYearLockRow`
- `JournalValidationSummary`
- `GstAccountingBridgeSummary`
- `AccountingAuditRow`
- `AccountingAuditDetail`
- `GstReturnDraftSummaryDto`
- `GstHsnSummaryReport`

## Safety

This command reads source files only. It does not call the API and cannot mutate ledgers, vouchers, journals, bank transactions, GST returns, or financial-year locks.
