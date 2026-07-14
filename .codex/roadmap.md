# Codex Roadmap

## Accounting Unification And Final Accounts Integration

The Balance Sheet / Final Accounts work is now merged into `version6`, and future work should avoid creating a second accounting universe. Existing Sale, Purchase, Voucher, Salary Payment, Customer, Vendor, Party, Ledger and Ledger Group data must become the canonical accounting source.

Planned stages:

1. **BS-16 - Accounting Master Audit**
   - Audit existing ledger groups, ledgers, parties, customers, vendors, salary payments, vouchers and module posting paths.
   - Create a database backup first using stage name `BS16AccountingMasterAudit`.
   - Produce a migration/unification design before code mutates accounting data.

2. **BS-17 - Indian/Tally-Compatible COA Normalization**
   - Align ledger groups with Indian accounting practice and TallyPrime/BUSY/Marg-style Chart of Accounts.
   - Prefer operational Indian COA grouping and Schedule III-ready reporting templates; do not force Ind AS unless legally required.

3. **BS-18 - Party And Ledger Unification**
   - Unify Customer, Vendor, Employee and Other Party identity.
   - One party may have multiple roles.
   - Each party role should resolve to a canonical ledger.

4. **BS-19 - Transaction Backfill And Ledger Reconciliation**
   - Reconcile existing Sale, Purchase, Voucher, Salary Payment, GST, Inventory and Party balances into canonical ledgers.
   - Use dry-run and reconciliation evidence before any live mutation.

5. **BS-20 - Final Accounts Direct Ledger Integration**
   - Final Accounts should read canonical ledger/accounting data directly.
   - Mapping becomes an exception/fallback tool, not required daily setup.

6. **BS-21 - Migration Safety, Rollback And Restore Drills**
   - Validate every backup can be listed and restore-planned from `Backupfilehistory.md`.
   - Run non-production restore drill before production migration.

## Backup Protocol

Every stage above must follow `docs/database-stage-backup-protocol.md`.

Standard command:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit
```
