# Stage 13D.5 Books Posting Preflight

Version: 5.13.29
Branch: Version5

## Scope

This stage adds a controlled live posting preflight without creating vouchers or journals. It verifies posting request DTO fields and reads the master data prerequisites needed before future audited posting flows are enabled in the modular Books app.

## Commands

Dry run:

```powershell
npm.cmd run modular:books:posting-preflight
```

Live prerequisite check:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:books:posting-preflight -- --live --require-token --strict-permissions
```

Use `--return-period=202606` to check a specific GST return period.

## Covered Posting Contracts

- `VoucherSaveRequest`
- `BankTransactionSaveRequest`
- `GstAccountingPostRequest`
- `FinancialYearLockSaveRequest`

## Live Read Prerequisites

- `GET /api/vouchers`
- `GET /api/ledgers`
- `GET /api/parties`
- `GET /api/bank-accounts`
- `GET /api/accounting/journal/validation`
- `GET /api/gst-returns/accounting-summary`

## Safety

- `POST /api/vouchers` is intentionally not called.
- `POST /api/accounting/bank-transactions` is intentionally not called.
- GST accounting post endpoints are intentionally not called.
- Financial-year lock save endpoints are intentionally not called.
- No accounting or ledger balance is changed.
