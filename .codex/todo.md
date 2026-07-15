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

- [~] **BS-17 - Indian/Tally-Compatible COA Normalization**
  - [~] Create database backup with stage `BS17IndianCOANormalization` on the deployed SRP host.
  - [x] Add read-only preview endpoint mapping current ledger groups to Indian/Tally-style primary groups.
  - [x] Identify duplicate, ambiguous and unmapped groups in preview output.
  - [x] Define required control-account candidates for debtors, creditors, cash, bank, GST, inventory, payroll and capital.
  - [x] Draft normalization and rollback plans in preview output/docs.
  - [ ] After this commit is pulled on SRP, run `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS17IndianCOANormalization`, deploy, then capture `GET /api/final-accounts/audit/coa-normalization`.

- [~] **BS-18 - Party And Ledger Unification**
  - [~] Create database backup with stage `BS18PartyLedgerUnification` on the deployed SRP host.
  - [x] Add read-only preview endpoint for unified Party role identity.
  - [x] Preview Customer/Vendor/Employee/Other Party to canonical Party/Ledger links.
  - [x] Detect duplicate party rows and party-ledger candidates.
  - [x] Draft safe merge/relink and rollback plan in preview output/docs.
  - [ ] After this commit is pulled on SRP, run `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS18PartyLedgerUnification`, deploy, then capture `GET /api/final-accounts/audit/party-ledger-unification`.

- [~] **BS-19 - Transaction Backfill And Ledger Reconciliation**
  - [~] Create database backup with stage `BS19TransactionBackfillReconciliation` on the deployed SRP host.
  - [x] Add read-only evidence endpoint for existing transaction backfill dry-run.
  - [x] Compare source totals to linked Final Accounts journal totals.
  - [x] Produce Trial Balance, Balance Sheet, Profit & Loss and source-control evidence in one response.
  - [x] Keep live mutation blocked without approved evidence.
  - [ ] After this commit is pulled on SRP, run `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS19TransactionBackfillReconciliation`, deploy, then capture `GET /api/final-accounts/audit/transaction-backfill-reconciliation`.

- [~] **BS-20 - Final Accounts Direct Ledger Integration**
  - [~] Create database backup with stage `BS20FinalAccountsLedgerIntegration` on the deployed SRP host.
  - [x] Add read-only direct-ledger evidence endpoint over canonical Books ledgers and journal lines.
  - [x] Compare canonical Books ledger Trial Balance, Profit & Loss and Balance Sheet values against Final Accounts reports.
  - [x] Report active Final Accounts mappings as exception-only review evidence.
  - [x] Report source posting balance by `JournalEntry.SourceType`.
  - [ ] After this commit is pulled on SRP, run `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS20FinalAccountsLedgerIntegration`, deploy, then capture `GET /api/final-accounts/audit/direct-ledger-integration`.

- [ ] **BS-21 - Restore Drill And Production Safety**
  - [ ] Restore latest stage backup to a non-production database.
  - [ ] Confirm backup history can identify a safe rollback point.
