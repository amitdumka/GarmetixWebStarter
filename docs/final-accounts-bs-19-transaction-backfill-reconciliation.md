# Final Accounts BS-19 Transaction Backfill Reconciliation Evidence

Date: 2026-07-15  
Stage: `BS19TransactionBackfillReconciliation`  
Status: code-only read-only evidence tooling

## Purpose

BS-19 prepares transaction backfill evidence before any live posting or direct ledger integration.

This stage does not create sync jobs, post journals, relink source records or mutate source data. It produces a reviewable evidence pack combining:

- read-only dry-run source coverage;
- source-to-Final Accounts reconciliation;
- Trial Balance equality evidence;
- Balance Sheet equality evidence;
- Profit & Loss tie-out figures;
- module exception guidance;
- approval gates and rollback plan.

## Mandatory Remote Backup

Run this on the deployed-host network before deploying this stage or capturing live evidence:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS19TransactionBackfillReconciliation
```

Expected backup location:

```text
/opt/garmetix/backup/database/
```

Expected history file:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
```

Do not run live backfill, journal posting, schema repair or ledger relinking until this backup exists and the evidence pack has been reviewed.

## Endpoint

```text
GET /api/final-accounts/audit/transaction-backfill-reconciliation
```

Optional query parameters:

```text
companyId
storeGroupId
storeId
from
to
modules
```

`modules` is a comma-separated list, for example:

```text
Sales,Purchase,CashBank,Payroll,Gst,Inventory
```

When `modules` is omitted, the existing Final Accounts default module set is used.

## Evidence Output

The response includes:

- dry-run backfill response;
- reconciliation response;
- statement evidence summary;
- module evidence rows;
- issue list;
- approval gates;
- rollback plan.

## Safety

- `WritesData = false`
- No `SaveChangesAsync`
- No sync job creation
- No source table mutation
- No Final Accounts journal posting
- No source posting link creation
- No ledger/group/party rewrite
- No live mutation

## Approval Gates

Before any live mutation:

1. BS-19 backup must exist in `Backupfilehistory.md`.
2. Dry-run evidence must be captured for the approved date range/modules.
3. Source hash drift must be resolved or explicitly approved.
4. Source totals and linked journal totals must be reviewed by module.
5. Trial Balance difference must be zero.
6. Balance Sheet difference must be zero.
7. P&L figures and mapping issues must be reviewed by Amit/CA.
8. Human approval must exist before live backfill.

## Next Stages

- BS-20: Final Accounts direct ledger integration.
- BS-21: restore drill and production safety.
