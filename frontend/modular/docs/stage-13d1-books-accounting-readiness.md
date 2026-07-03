# Stage 13D.1 Books Accounting Readiness

Version: 5.13.25
Branch: Version5

## Scope

Stage 13D starts the Books/accounting hardening lane. This slice adds a repeatable readiness check for the modular Books app routes and the shared ASP.NET API endpoints used by accounting master data, ledgers, vouchers, petty cash, bank operations, audit, message logs, financial year locks and GST accounting summary.

This stage is read-only. It does not create vouchers, post journals, alter ledger balances, create bank transactions, close financial years, or change GST records.

## Commands

Dry run from the repository root:

```powershell
npm.cmd run modular:books:accounting-readiness
```

Live local API check with an accountant/admin-capable token:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:books:accounting-readiness -- --live --require-token --strict-permissions
```

Public tunnel check:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:books:accounting-readiness -- --live --mode=public --require-token --strict-permissions
```

Optional bank statement and reconciliation checks:

```powershell
npm.cmd run modular:books:accounting-readiness -- --live --bank-account-id=<bank-account-guid>
```

Use `--return-period=202606` to check a specific GST return period.

## Covered API Surface

- `GET /api/ledger-groups`
- `GET /api/ledgers`
- `GET /api/parties`
- `GET /api/banks`
- `GET /api/bank-accounts`
- `GET /api/vouchers`
- `GET /api/petty-cash-sheets`
- `GET /api/accounting/trial-balance`
- `GET /api/accounting/ledger-sync/status`
- `GET /api/accounting/audit/recent`
- `GET /api/accounting/message-logs`
- `GET /api/accounting/bank-transactions`
- `GET /api/cheque-logs`
- `GET /api/accounting/financial-year-locks`
- `GET /api/accounting/journal/validation`
- `GET /api/gst-returns/accounting-summary`
- Optional: `GET /api/accounting/bank-statement/{bankAccountId}`
- Optional: `GET /api/accounting/bank-reconciliation/{bankAccountId}`

## Safety Rules

- One shared ASP.NET Core API remains the only backend.
- One PostgreSQL database remains the only database.
- Regular accounting and off-book cash voucher flows remain separated.
- No POST, PUT or DELETE request is made by this readiness script.
- Permission failures are warnings by default so route presence can be checked with limited tokens. Use `--strict-permissions` for acceptance.
- Missing routes and server errors fail the check because they indicate a broken contract.

## Next Follow-Ups

- Stage 13D.2: add Books accounting contract checks for voucher, ledger, party, bank account, audit and GST summary field expectations.
- Stage 13D.3: add safe browser acceptance checks for vouchers, petty cash, cash details, audit and GST pages on 14 inch laptop layouts.
- Stage 13D.4: add ledger/party/bank-account sync readiness validation to keep hidden party/bank ledgers internal.
- Stage 13D.5: add controlled live posting readiness preflight without creating vouchers or journals.
