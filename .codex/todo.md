# Codex Todo

## Backup And Deployment Safety

- [x] Add stage-aware SRP database backup script.
- [x] Store backups under `/opt/garmetix/backup/database/`.
- [x] Create/update `/opt/garmetix/backup/database/Backupfilehistory.md`.
- [x] Add automatic pre-deploy backup into SRP whole-site deploy.
- [x] Add npm command to list latest backups/history.
- [x] Document backup/restore protocol for Codex and Claude Code.

## Accounting Unification Future Work

- [~] **BS-16 - Accounting Master Audit**
  - [~] Create database backup with stage `BS16AccountingMasterAudit` on the deployed SRP host.
  - [x] Add read-only audit endpoint for existing ledger groups and ledgers.
  - [x] Add read-only audit endpoint coverage for customers, vendors, employees and other parties.
  - [x] Add read-only audit endpoint coverage for posting sources: Sale, Purchase, Voucher, Salary Payment, GST, Inventory, Cash/Bank and Party settlements.
  - [x] Produce a no-mutation design report.
  - [ ] After this commit is pulled on SRP, run `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit`, deploy, then capture the live audit result.

- [ ] **BS-17 - Indian/Tally-Compatible COA Normalization**
  - [ ] Map current ledger groups to Indian/Tally-style primary groups.
  - [ ] Identify duplicate or ambiguous groups.
  - [ ] Draft normalization rules and rollback plan.

- [ ] **BS-18 - Party And Ledger Unification**
  - [ ] Design unified Party role model.
  - [ ] Link Customer/Vendor/Employee/Other Party to canonical ledger.
  - [ ] Prevent duplicate party ledgers.

- [ ] **BS-19 - Transaction Backfill And Ledger Reconciliation**
  - [ ] Dry-run existing transaction backfill.
  - [ ] Produce Trial Balance, Balance Sheet and source-control reconciliation.
  - [ ] Do not mutate production without approved evidence.

- [ ] **BS-20 - Final Accounts Direct Ledger Integration**
  - [ ] Read canonical ledger data directly.
  - [ ] Keep mapping only for exceptions.

- [ ] **BS-21 - Restore Drill And Production Safety**
  - [ ] Restore latest stage backup to a non-production database.
  - [ ] Confirm backup history can identify a safe rollback point.
