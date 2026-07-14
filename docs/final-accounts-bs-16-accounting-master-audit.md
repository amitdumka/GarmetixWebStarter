# Final Accounts BS-16 Accounting Master Audit

Date: 2026-07-14  
Stage: `BS16AccountingMasterAudit`  
Status: code-only read-only audit tooling

## Purpose

BS-16 audits the existing accounting masters and operational source modules before any unification or migration work. It is intentionally read-only.

The goal is to confirm how the current Books accounting masters relate to:

- ledger groups;
- ledgers;
- parties;
- customers;
- vendors;
- sales invoices;
- purchase invoices;
- vouchers;
- cash vouchers;
- salary payments;
- Final Accounts mappings and source posting links.

## Mandatory Remote Backup

This local development machine cannot reach the remote SRP host, so the deployed-host backup must be run from the remote network before deploying this stage or running the live audit.

Run on the remote-capable system:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit
```

Expected backup location:

```text
/opt/garmetix/backup/database/
```

Expected history file:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
```

Do not run live data migration, schema repair, backfill or production deployment until that backup history entry exists.

## Endpoint

```text
GET /api/final-accounts/audit/accounting-master
```

Optional query parameters:

```text
companyId
storeGroupId
storeId
```

The endpoint is registered outside the Final Accounts enabled-module guard so an authorized admin/owner can audit readiness before enabling Final Accounts.

## Response Areas

The response includes:

- master counts;
- source coverage counts and totals;
- pending Final Accounts link counts;
- missing ledger/group/party links;
- duplicate customer/vendor/party/ledger signals;
- recommendations for BS-17 through BS-20.

## Safety

- `WritesData = false`
- No `SaveChangesAsync`
- No EF migration
- No schema repair
- No source table mutation
- No journal posting
- No Final Accounts backfill

## Next Stages

- BS-17: Indian/Tally-compatible Chart of Accounts normalization.
- BS-18: Party/customer/vendor/employee ledger unification.
- BS-19: Transaction backfill and reconciliation.
- BS-20: Final Accounts direct ledger integration.
- BS-21: restore drill and production safety.
