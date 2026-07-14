# Final Accounts BS-17 COA Normalization Preview

Date: 2026-07-15  
Stage: `BS17IndianCOANormalization`  
Status: code-only read-only preview tooling

## Purpose

BS-17 prepares the existing Books ledger groups for Indian operational accounting practice and TallyPrime/BUSY/Marg-style Chart of Accounts grouping.

This stage does not rewrite ledger groups. It produces a reviewable preview that maps current Books ledger groups into Indian/Tally-style primary groups and identifies gaps that must be approved before any migration.

## Mandatory Remote Backup

Run this on the deployed-host network before deploying this stage or capturing live evidence:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS17IndianCOANormalization
```

Expected backup location:

```text
/opt/garmetix/backup/database/
```

Expected history file:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
```

Do not run any live COA migration, ledger relinking, schema repair or backfill until this backup exists and the preview has been reviewed.

## Endpoint

```text
GET /api/final-accounts/audit/coa-normalization
```

Optional query parameters:

```text
companyId
storeGroupId
storeId
```

The endpoint is registered outside the Final Accounts enabled-module guard so authorized admins can audit Books accounting masters before enabling direct Final Accounts integration.

## Preview Output

The response includes:

- summary counts;
- current ledger-group to Indian/Tally-style primary-group mappings;
- confidence score and rule code for every mapped group;
- duplicate normalized group names;
- required control-account candidates;
- blocking/warning issues;
- migration review plan;
- rollback plan.

## Indian/Tally-Style Primary Groups

The preview uses code-defined rules for common primary groups such as:

- Capital Account
- Reserves & Surplus
- Secured Loans
- Unsecured Loans
- Current Liabilities
- Duties & Taxes
- Sundry Creditors
- Fixed Assets
- Investments
- Current Assets
- Bank Accounts
- Cash-in-Hand
- Sundry Debtors
- Stock-in-Hand
- Sales Accounts
- Purchase Accounts
- Direct Incomes
- Indirect Incomes
- Direct Expenses
- Indirect Expenses
- Suspense Account

These are operational grouping targets for Indian accounting software compatibility. They are not a blind Ind AS conversion.

## Control Accounts

The preview searches existing ledgers for candidates required by posting and Final Accounts direct integration:

- Customer Receivables
- Vendor Payables
- Cash In Hand
- Bank Account
- Output/Input GST ledgers
- Inventory Stock
- Salary Payable
- Payroll Expense
- Owner Capital

Missing or group-mismatched candidates are warnings, not automatic create/update actions.

## Safety

- `WritesData = false`
- No `SaveChangesAsync`
- No EF migration
- No schema repair
- No ledger group rewrite
- No ledger relinking
- No journal posting
- No source-table mutation
- No live mutation

## Next Stages

- BS-18: party/customer/vendor/employee ledger unification.
- BS-19: transaction backfill and reconciliation.
- BS-20: Final Accounts direct ledger integration.
- BS-21: restore drill and production safety.
