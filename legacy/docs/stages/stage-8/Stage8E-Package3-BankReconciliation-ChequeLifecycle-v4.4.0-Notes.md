# Stage 8E Package 3 - Bank Reconciliation and Cheque Lifecycle (v4.4.0)

This package completes the next Stage 8E accounting hardening item after split-payment posting and the clean production migration baseline.

## Implemented

- Bank statement lines now keep reconciliation operator, date, reference, remarks, and linked bank transaction metadata.
- Bank transactions now mirror reconciliation state so settlement entries can be filtered by open/reconciled status.
- Accounting API now exposes a bank reconciliation summary plus reconcile and reopen actions.
- Cheque logs now preserve bank-transaction links and lifecycle timestamps for issued, deposited, cleared, bounced, and cancelled states.
- Accounting UI now includes a Bank Reconciliation tab, open-line metric, statement-line reconcile/reopen actions, and quick cheque clear/bounce actions.
- Clean fresh-install schema mode remains the production default for the Mac mini Docker deployment.

## Acceptance checks

- Fresh database should create the new fields through `EnsureCreated()` because they are part of the current `GarmetixDbContext` model.
- Existing development/test databases get idempotent schema-repair columns through `DatabaseSchemaRepairService`.
- Statement line reconciliation can be reopened without deleting the original line or changing ledger values.
- Cheque clear/bounce/cancel changes status and timestamp only; it does not alter accounting amount by itself.
