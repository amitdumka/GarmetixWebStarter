# Final Accounts BS-18 Party Ledger Unification Preview

Date: 2026-07-15  
Stage: `BS18PartyLedgerUnification`  
Status: code-only read-only preview tooling

## Purpose

BS-18 prepares one canonical party-ledger model for Customers, Vendors, Employees and Other Parties.

This stage does not relink records or merge ledgers. It produces a reviewable preview showing how operational roles currently connect to accounting Party and Ledger masters.

## Mandatory Remote Backup

Run this on the deployed-host network before deploying this stage or capturing live evidence:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS18PartyLedgerUnification
```

Expected backup location:

```text
/opt/garmetix/backup/database/
```

Expected history file:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
```

Do not run live party relinking, ledger merging, schema repair or backfill until this backup exists and the preview has been reviewed.

## Endpoint

```text
GET /api/final-accounts/audit/party-ledger-unification
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
- Customer/Vendor/Employee/OtherParty role link status;
- canonical identity groups using GSTIN, PAN, mobile or normalized name;
- current Party and Ledger references;
- candidate Party and Ledger references;
- duplicate Party groups;
- missing Party/Ledger issues;
- party-ledger unification plan;
- rollback plan.

## Identity Key Priority

The preview uses the strongest available identity in this order:

1. GSTIN
2. PAN
3. Mobile/phone last 10 digits
4. Normalized name

These keys are review aids only. They are not automatic merge approval.

## Safety

- `WritesData = false`
- No `SaveChangesAsync`
- No EF migration
- No schema repair
- No `Customer.PartyId` update
- No `Vendor.PartyId` update
- No Employee party creation
- No Party merge
- No Ledger merge
- No journal posting
- No source-table mutation
- No live mutation

## Next Stages

- BS-19: transaction backfill and reconciliation.
- BS-20: Final Accounts direct ledger integration.
- BS-21: restore drill and production safety.
