# Codex Pending - August 2026

## Current Instruction

- Do not remove or purge pre-July sale invoices again. Amit confirmed fresh data has been imported and those invoices must now be kept.
- Continue the Final Accounts / Balance Sheet accounting-unification context from the earlier BS-16 through BS-21 handoff.
- Do not start new live accounting mutation unless Amit gives a fresh explicit approval and a new SRP backup is taken first.
- Stop before any COA normalization, party or ledger relink, transaction source mutation, report-source switch, or production restore.

## Current Branch And SRP Target

- Local branch: `version7`.
- Latest pushed commit observed on 2026-08-04: `21e6190 fix(final-accounts): resolve inventory BS19 import backfill`.
- Public API observed version: `7.0.1`.
- Public API observed stage: `Version7: BS-19 approved Final Accounts transaction backfill executor`.
- Public API observed build code: `GARMETIX-V7-20260719-701`.
- SRP host for current office network: prefer `192.168.11.94`; `192.168.12.8` can also work. The old `192.168.11.127` target is not reliable from this workstation.
- Temporary deploy/env helpers are under `outputs/final-accounts-evidence/` and are intentionally local evidence tooling.

## Completed On 2026-08-03

- Took SRP backup for `BS19PostingMappingSetup`.
- Created approved Final Accounts catalog and posting mapping setup only:
  - Groups created: `13`.
  - Accounts created: `43`.
  - Mappings created: `78`.
  - Validation issues after setup: `0`.
  - No Books ledger groups, ledgers, source rows, party links, COA normalization, report source, or production restore were changed in that setup.
- Took SRP backup for `BS19ApprovedTransactionBackfillRerun`.
- First BS-19 backfill rerun failed because the Final Accounts financial year and period were still `Draft`.
- Took SRP backup for `BS19FiscalPeriodOpenSetup`.
- Opened the existing Final Accounts FY/period:
  - FY `2026-27`: `Draft` to `Open`.
  - Period `2026-2027`: `Draft` to `Open`.
  - Validation issues: `0`.
- Took SRP backup for `BS19ApprovedTransactionBackfillRerun2`.
- Reran the approved BS-19 backfill:
  - Job: `FA-SYNC-6803FC0F`.
  - Status: `CompletedWithErrors`.
  - Source count: `902`.
  - Posted/linked: `852`.
  - Failed/pending: `50`.
  - Drift count: `0`.
- Captured BS-19 and BS-20 read-only evidence after the rerun.
- Captured cleanup, inventory, sales, purchase and accounting totals after the rerun.

## Completed On 2026-08-04

- Pulled current branch: `version7` was already up to date with `origin/version7`.
- Ran local Final Accounts readiness check: passed.
- Took fresh SRP backup for `PostSaleImportAccountingEvidenceRefresh`:
  - `garmetix-srp-db-20260804-010803-IST-PostSaleImportAccountingEvidenceRefresh-v6.9.43.dump`.
  - `swalekha-srp-db-20260804-010803-IST-PostSaleImportAccountingEvidenceRefresh-v6.9.43.dump`.
- Captured refreshed live read-only BS-16 through BS-20 evidence after historical sale import cleanup.
- Captured refreshed cleanup, inventory, sales, purchase and accounting totals.
- Ran BS-21 restore drill. First run without explicit `--backup-file` selected the same-timestamp Swalekha dump and failed API smoke because that DB has no Garmetix `Users` table. Valid rerun with explicit Garmetix backup passed:
  - Backup: `garmetix-srp-db-20260804-010803-IST-PostSaleImportAccountingEvidenceRefresh-v6.9.43.dump`.
  - Restore DB: `garmetix_restore_drill_bs21restoredrillproducti_20260804_010930`.
  - SQL tables: `183`.
  - App smoke: `Passed`.
  - Restore DB kept: `false`.
- Captured `/opt/garmetix/backup/database/Backupfilehistory.md` and `/opt/garmetix/backup/database/RestoreDrillHistory.md`.
- Created Amit/CA current evidence summary:
  - `outputs/final-accounts-evidence/20260803-193811-current-bs16-bs20-after-sale-import/amit-ca-current-evidence-summary.md`.
- Amit approved BS-20 manual ledger group classification as evidence-only:
  - `Employees` as Employee Advances / Loans & Advances under Current Assets.
  - `No Group` as Suspense Account for old one-time vouchers only.
  - Approval explicitly excludes ledger move, COA normalization and report-source switch.
  - Evidence note: `outputs/final-accounts-evidence/20260803-193811-current-bs16-bs20-after-sale-import/bs20-manual-ledger-group-classification-approval.md`.
- No COA normalization, party/ledger relink, source mutation, report-source switch, or production restore was run.
- Captured current BS-19 pending/drift row detail read-only:
  - `outputs/final-accounts-evidence/20260803-200017-bs19-current-pending-drift-detail`.
  - Counts matched BS-19 endpoint exactly.
  - Sales pending: `42`, amount `146,682.00`.
  - CashBank pending: `50`, amount `146,682.00`.
  - Inventory pending: `91`, amount `174,381.42`.
  - Inventory drift: `5`, amount `8,090.25`.
  - Review note: `outputs/final-accounts-evidence/20260803-200017-bs19-current-pending-drift-detail/bs19-current-pending-drift-review-for-amit.md`.
- Amit approved BS-19 Sales and CashBank live backfill only for pending historical Vyapar sale invoices/payments, with fresh SRP backup first. Explicit exclusions: Inventory, COA normalization, party/ledger relink, report-source switch and production restore.
- Pulled current branch again: `version7` was already up to date with `origin/version7`.
- Took fresh SRP backup for `BS19SalesCashBankBackfillOnly`:
  - `garmetix-srp-db-20260804-013753-IST-BS19SalesCashBankBackfillOnly-v6.9.43.dump`.
  - `swalekha-srp-db-20260804-013753-IST-BS19SalesCashBankBackfillOnly-v6.9.43.dump`.
- Ran approved BS-19 Sales/CashBank-only backfill:
  - Job: `FA-SYNC-4C0E1910`.
  - Status: `Completed`.
  - Source count: `906`.
  - Skipped existing: `814`.
  - Posted: `92` (`42` Sales and `50` CashBank).
  - Failed: `0`.
  - Drift: `0`.
- Captured post-backfill BS-16 through BS-20 evidence:
  - `outputs/final-accounts-evidence/20260803-200942-current-bs16-bs20-after-sale-import`.
- Captured post-backfill cleanup totals:
  - `outputs/final-accounts-evidence/20260803-200942-cleanup-after-bs19-backfill`.
- Captured post-backfill BS-19 pending/drift detail:
  - `outputs/final-accounts-evidence/20260803-200955-bs19-current-pending-drift-detail`.
- Completion evidence note:
  - `outputs/final-accounts-evidence/20260803-200924-bs19-sales-cashbank-approved-backfill/bs19-sales-cashbank-backfill-completion-evidence.md`.
- Prepared read-only BS-19 Sales difference plus Inventory pending/drift explanation:
  - `outputs/final-accounts-evidence/20260803-202137-bs19-sales-inventory-explanation/bs19-sales-inventory-explanation.md`.
  - Sales difference `125,186.05` is explained by Sales Discount debit `125,183.51` plus Sales Rounding debit `2.54`; Sales invoices are fully linked `220/220`.
  - Inventory remains the only pending/drift module: pending `91` amount `174,381.42`, drift `5` amount `8,090.25`.
- Amit accepted the BS-19 Sales difference as discount/rounding-off presentation:
  - Evidence note: `outputs/final-accounts-evidence/20260803-202137-bs19-sales-inventory-explanation/bs19-sales-difference-approval.md`.
  - No Sales/CashBank mutation remains pending from this explanation.
  - No database mutation, COA normalization, party/ledger relink, Inventory backfill, report-source switch or production restore was run for this approval.
- Deployed Inventory BS-19 import backfill adapter fix to SRP `192.168.11.94`:
  - Commit: `21e6190 fix(final-accounts): resolve inventory BS19 import backfill`.
  - Release verified live: `/opt/garmetix-srp/releases/20260803203530`.
  - `final-accounts` page returned HTTP 200.
  - `api/final-accounts/status` and `api/final-accounts/sync/options` returned HTTP 401, proving auth-gated routes are registered.
- Took mandatory SRP backup for `BS19InventoryResolutionAdapterDeploy` before deploy:
  - `garmetix-srp-db-20260804-021047-IST-BS19InventoryResolutionAdapterDeploy-v6.9.43.dump`.
  - `swalekha-srp-db-20260804-021047-IST-BS19InventoryResolutionAdapterDeploy-v6.9.43.dump`.
- Took mandatory SRP backup for `BS19InventoryResolutionBackfillAndDriftHashRefresh` before live hash refresh/backfill:
  - `garmetix-srp-db-20260804-021523-IST-BS19InventoryResolutionBackfillAndDriftHashRefresh-v6.9.43.dump`.
  - `swalekha-srp-db-20260804-021523-IST-BS19InventoryResolutionBackfillAndDriftHashRefresh-v6.9.43.dump`.
- Ran guarded Inventory drift hash refresh:
  - Evidence: `outputs/final-accounts-evidence/20260803-204757-bs19-inventory-drift-hash-refresh`.
  - Exactly `5` Inventory source posting link hashes refreshed.
  - Drift rows are now `0`.
- Ran approved Inventory-only BS-19 backfill:
  - Evidence: `outputs/final-accounts-evidence/20260803-204823-bs19-inventory-approved-backfill`.
  - Job: `FA-SYNC-09732B3D`.
  - Status: `CompletedWithErrors`.
  - Source rows: `81`.
  - Posted rows: `58`, amount `126,016.22`.
  - Skipped existing rows: `14`, amount `26,995.60`.
  - Failed rows: `9`, amount `10,170.20`.
  - Drift rows: `0`.
- Captured post-run BS-16 through BS-20 evidence:
  - `outputs/final-accounts-evidence/20260803-204918-current-bs16-bs20-after-sale-import`.
- Captured post-run cleanup, inventory, sales, purchase and accounting totals:
  - `outputs/final-accounts-evidence/20260803-204917-cleanup-after-bs19-backfill`.
- Captured BS-19 sync job evidence:
  - `outputs/final-accounts-evidence/20260803-204927-bs19-sync-job-summary`.
- Created Inventory resolution completion evidence note:
  - `outputs/final-accounts-evidence/20260803-204823-bs19-inventory-approved-backfill/bs19-inventory-resolution-completion-evidence.md`.

## Evidence Paths

- `outputs/final-accounts-evidence/20260803-123336-bs19-posting-mapping-setup`
- `outputs/final-accounts-evidence/20260803-123432-bs19-approved-backfill-rerun`
- `outputs/final-accounts-evidence/20260803-123701-finalaccounts-periods-before`
- `outputs/final-accounts-evidence/20260803-123753-bs19-fiscal-period-open-setup`
- `outputs/final-accounts-evidence/20260803-123813-bs19-approved-backfill-rerun`
- `outputs/final-accounts-evidence/20260803-124159-bs19-sync-job-summary`
- `outputs/final-accounts-evidence/20260803-124248-post-bs19-backfill-rerun`
- `outputs/final-accounts-evidence/20260803-124253-cleanup-after-bs19-backfill`
- `outputs/final-accounts-evidence/20260803-193811-current-bs16-bs20-after-sale-import`
- `outputs/final-accounts-evidence/20260803-193820-cleanup-after-bs19-backfill`
- `outputs/final-accounts-evidence/20260803-194048-bs21-backup-restore-history`
- `outputs/final-accounts-evidence/20260803-200017-bs19-current-pending-drift-detail`
- `outputs/final-accounts-evidence/20260803-200924-bs19-sales-cashbank-approved-backfill`
- `outputs/final-accounts-evidence/20260803-200942-current-bs16-bs20-after-sale-import`
- `outputs/final-accounts-evidence/20260803-200942-cleanup-after-bs19-backfill`
- `outputs/final-accounts-evidence/20260803-200955-bs19-current-pending-drift-detail`
- `outputs/final-accounts-evidence/20260803-202137-bs19-sales-inventory-explanation`
- `outputs/final-accounts-evidence/20260803-204757-bs19-inventory-drift-hash-refresh`
- `outputs/final-accounts-evidence/20260803-204823-bs19-inventory-approved-backfill`
- `outputs/final-accounts-evidence/20260803-204917-bs19-current-pending-drift-detail`
- `outputs/final-accounts-evidence/20260803-204917-cleanup-after-bs19-backfill`
- `outputs/final-accounts-evidence/20260803-204918-current-bs16-bs20-after-sale-import`
- `outputs/final-accounts-evidence/20260803-204927-bs19-sync-job-summary`

## Latest SRP Backup Evidence

- `garmetix-srp-db-20260803-180057-IST-BS19PostingMappingSetup-v6.9.43.dump`
  - SHA256: `d0cf479bc5196790461df9af0cc51458475538ed25cbc47e090157e5403c79d0`
- `swalekha-srp-db-20260803-180057-IST-BS19PostingMappingSetup-v6.9.43.dump`
  - SHA256: `391425d70166e32e747a3ab4908bd8f4b610b09cbe162b9e7ec5f1cccfd22230`
- `garmetix-srp-db-20260803-180352-IST-BS19ApprovedTransactionBackfillRerun-v6.9.43.dump`
  - SHA256: `e0e59171c83e6180fc8d0f48e007f34544ca2814ac5ee1d36954a390ae505aa0`
- `garmetix-srp-db-20260803-180715-IST-BS19FiscalPeriodOpenSetup-v6.9.43.dump`
  - SHA256: `3b3e4874b72f9328dfd964e1e41e304a676bf4211dde54f348adaa09d9d75ea0`
- `garmetix-srp-db-20260803-180801-IST-BS19ApprovedTransactionBackfillRerun2-v6.9.43.dump`
  - SHA256: `ccc9ddfc73a6bb5232ea00b34dbe2190eb0df1097602015991e0724146cf62f0`
- `garmetix-srp-db-20260804-013753-IST-BS19SalesCashBankBackfillOnly-v6.9.43.dump`
- `swalekha-srp-db-20260804-013753-IST-BS19SalesCashBankBackfillOnly-v6.9.43.dump`
- `garmetix-srp-db-20260804-021047-IST-BS19InventoryResolutionAdapterDeploy-v6.9.43.dump`
- `swalekha-srp-db-20260804-021047-IST-BS19InventoryResolutionAdapterDeploy-v6.9.43.dump`
- `garmetix-srp-db-20260804-021523-IST-BS19InventoryResolutionBackfillAndDriftHashRefresh-v6.9.43.dump`
- `swalekha-srp-db-20260804-021523-IST-BS19InventoryResolutionBackfillAndDriftHashRefresh-v6.9.43.dump`

## Current BS-19 Result

- Read-only endpoint: `/api/final-accounts/audit/transaction-backfill-reconciliation`.
- Source count: `1011`.
- Already linked: `1002`.
- Pending: `9`.
- Drift count: `0`.
- Source total: `2,971,906.16`.
- Reconciliation difference: `-115,015.85`.
- Issues:
  - `ModuleDifferences`: `2`.
  - `ReconciliationDifference`: `1`.
  - `PendingBackfillRows`: `9`.
- Module evidence:
  - `CashBank`: source count `686`, pending `0`, drift `0`, difference `0.00`, status `Balanced`.
  - `Inventory`: source count `81`, pending `9`, drift `0`, difference `10,170.20`, status `Difference`.
  - `Payroll`: source count `23`, pending `0`, difference `0`, status `Balanced`.
  - `Purchase`: source count `1`, pending `0`, difference `0`, status `Balanced`.
  - `Sales`: source count `220`, pending `0`, drift `0`, difference `-125,186.05`, status `Difference`.

## Current BS-20 Result

- Read-only endpoint: `/api/final-accounts/audit/direct-ledger-integration`.
- Books ledger groups: `32`.
- Books ledgers: `227`.
- Posted journal lines: `2874`.
- Trial Balance period debit: `4,101,602.99`.
- Trial Balance period credit: `4,101,602.99`.
- Profit after tax: `-268,910.58`.
- Balance Sheet assets: `1,440,655.14`.
- Balance Sheet liabilities and equity: `54,738.98`.
- Active exception mappings: `78`.
- Blocking issues: `2`.
- Issues:
  - `ManualLedgerGroupClassification`: still reported by endpoint; Amit approved evidence-only classification for Employees and No Group, but no ledger move/COA normalization was run.
  - `StatementDifference`: `10`.
  - `ExceptionMappingsRemain`: `78`.

## Cleanup And Data Totals

- Active cancelled sale invoices: `0`.
- Active July sale invoices: `14`.
- Active July sale bill amount: `59,458.00`.
- Active July paid amount: `59,458.00`.
- Sales payment difference: `0`.
- Active purchase invoices: `19`.
- Purchase payment difference: `2,461,313.70`.
- Zero purchase stock rows: `2`.
- Active pre-July sale invoices observed: `680`.
- Active pre-July sale bill amount observed: `2,409,769.14`.
- Active stock rows: `1559`.
- Negative current stock rows: `4`.
- FY journal entries: `813`.
- FY journal lines: `2874`.
- FY debit: `4,101,602.99`.
- FY credit: `4,101,602.99`.
- FY accounting difference: `0`.
- Important: pre-July sale invoices are now approved to keep. Do not purge them.

## Pending Work

- P0: Amit/CA review and approval of refreshed BS-16 through BS-21 evidence.
- P1: Review or correct the `9` remaining BS-19 Inventory rows before rerunning Inventory backfill:
  - Inventory pending `9`, drift `0`, difference `10,170.20`.
  - `8` rows are blocked by missing stock cost evidence.
  - `1` row is blocked by negative stock movement evidence.
- P1 recommendation: Sales, CashBank and most Inventory historical Vyapar sale invoices/payments are now linked. Do not bypass the missing-cost or negative-stock guards; fix stock evidence first, then take a fresh SRP backup and rerun Inventory-only BS-19 backfill.
- P1: Investigate the BS-19 reconciliation differences read-only first:
  - Inventory difference: `10,170.20`.
  - Sales difference: `-125,186.05` even though Sales pending/drift is now zero; Amit accepted this as discount plus debit round-off presentation.
  - Net reconciliation difference: `-115,015.85`.
- P1: Resolve or obtain Amit/CA approval for BS-20 direct-ledger blockers:
  - Manual ledger group classifications approved as evidence-only on 2026-08-04: `Employees` and `No Group`.
  - `10` statement differences.
  - `78` exception mappings still active.
- P2: Purchase payment difference remains expected because Amit confirmed purchase payments are not entered yet. Do not auto-create purchase payments.
- P2: Keep the `2` zero purchase stock rows as zero until Amit rectifies purchase invoices.
- P2: Do not purge pre-July sale invoices. They are fresh imported data and are intentionally retained.

## Hard Stop Rules

- No COA normalization until Amit/CA approval.
- No party or ledger relink until Amit/CA approval.
- No party merge, ledger merge, or Books master rewrite until Amit/CA approval.
- No new transaction/source mutation until a fresh explicit approval and a mandatory SRP backup.
- No Final Accounts report-source switch until BS-19 and BS-20 are clean or explicitly approved.
- No production restore. BS-21 restore drill is non-production only.

## Next Recommended Sequence

1. Correct or approve stock evidence for the 9 remaining Inventory COGS blockers:
   - Missing stock cost evidence: `AF/2025/1153`, `AF/2025/1157`, `AF/2025/1170`, `AF/2025/1180`, `AF/2025/1198`, `AF/2025/1209`, `AF/2025/1228`, `AFSS/202607/INV/0003`.
   - Negative stock movement evidence: `AFSS-202607-INV-0007`.
2. After those 9 are fixed, take a fresh SRP backup and rerun Inventory-only BS-19 backfill.
3. Keep purchase payment difference as expected until Amit enters purchase payments.
4. Only after BS-19 and BS-20 are clean or explicitly approved, decide whether to proceed with any COA normalization, relink, report-source switch or production restore.

## Useful Commands

```powershell
git status --short
git rev-parse --abbrev-ref HEAD
git log -1 --oneline
npm --prefix frontend/modular run final-accounts:readiness
```

```powershell
node frontend/modular/scripts/run-bash-script.mjs outputs/final-accounts-evidence/run_srp_backup_192_168_11_94.sh --stage=<StageName>
```

```powershell
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
```

If the restore-drill command defaults to the old SRP target, create or use a `192.168.11.94` env wrapper before running it.
