# Codex Changelog

## 2026-07-15 - BS-18 Party Ledger Unification Preview

Amit asked to go ahead with the next part. Added BS-18 as a read-only party-ledger unification preview so Customer, Vendor, Employee and Other Party records can be reviewed against canonical Party and Ledger masters before any relink or merge.

Files changed:

- `backend/Garmetix.Api/FinalAccounts/FinalAccountsPartyLedgerUnificationContracts.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsPartyLedgerUnificationRules.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsPartyLedgerUnificationService.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs`
- `backend/Garmetix.Api/Program.cs`
- `backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsPartyLedgerUnificationRulesTests.cs`
- `docs/final-accounts-bs-18-party-ledger-unification.md`
- `frontend/modular/scripts/final-accounts-readiness.mjs`
- `todo-balancesheet.md`, `.codex/todo.md`, `.claude/todo.md`

What changed:

- Added `GET /api/final-accounts/audit/party-ledger-unification`.
- Registered `FinalAccountsPartyLedgerUnificationService`.
- Builds identity keys from GSTIN, PAN, mobile or normalized name.
- Reviews Customer/Vendor/Employee/OtherParty role links to existing Party and Ledger rows.
- Reports missing parties, parties missing ledgers, candidate party links, duplicate party rows, multi-role identities and party-ledger flag mismatches.
- Returns unification and rollback plan steps for Amit/CA review.
- Keeps BS-18 explicitly no-mutation: no migration, schema repair, PartyId update, party creation, party merge, ledger merge, backfill, deploy or service restart from this workstation.
- Documents the required deployed-host backup command:
  `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS18PartyLedgerUnification`.

Validation:

- `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release -p:UseSharedCompilation=false`: passed with the same 7 pre-existing nullable warnings outside Final Accounts.
- `dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release -p:UseSharedCompilation=false`: 253 passed, 3 pre-existing Postgres-only skipped, 0 failed.
- `npm --prefix frontend/modular run final-accounts:readiness`: passed.
- `npm --prefix frontend/modular run check`: passed.
- `git diff --check`: passed.

Remote status: backup file/history row and live preview output are pending on the deployed SRP host.

## 2026-07-15 - BS-17 Indian/Tally COA Normalization Preview

Amit asked to continue with the next part. Added BS-17 as a read-only COA normalization preview so existing Books ledger groups can be reviewed against Indian operational accounting practice and TallyPrime/BUSY/Marg-style primary groups before any migration.

Files changed:

- `backend/Garmetix.Api/FinalAccounts/FinalAccountsCoaNormalizationContracts.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsCoaNormalizationRules.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsCoaNormalizationService.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs`
- `backend/Garmetix.Api/Program.cs`
- `backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsCoaNormalizationRulesTests.cs`
- `docs/final-accounts-bs-17-coa-normalization.md`
- `frontend/modular/scripts/final-accounts-readiness.mjs`
- `todo-balancesheet.md`, `.codex/todo.md`, `.claude/todo.md`

What changed:

- Added `GET /api/final-accounts/audit/coa-normalization`.
- Registered `FinalAccountsCoaNormalizationService`.
- Maps current Books ledger groups to Indian/Tally-style primary groups with confidence/rule codes.
- Reports duplicate/unmapped/low-confidence groups and required control-account candidates.
- Returns migration and rollback plan steps in the response for Amit/CA review.
- Keeps BS-17 explicitly no-mutation: no migration, schema repair, ledger rewrite, ledger relink, backfill, deploy or service restart from this workstation.
- Documents the required deployed-host backup command:
  `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS17IndianCOANormalization`.

Validation:

- `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release -p:UseSharedCompilation=false`: passed with the same 7 pre-existing nullable warnings outside Final Accounts.
- `dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release -p:UseSharedCompilation=false`: 240 passed, 3 pre-existing Postgres-only skipped, 0 failed.
- `npm --prefix frontend/modular run final-accounts:readiness`: passed.
- `npm --prefix frontend/modular run check`: passed.
- `git diff --check`: passed.

Remote status: backup file/history row and live preview output are pending on the deployed SRP host.

## 2026-07-14 - BS-16 Accounting Master Audit Tooling

Amit confirmed this workstation cannot deploy to the remote SRP network directly and asked to implement code locally so the deployed system can pull and run the backup/deploy script there. Added BS-16 as a read-only Final Accounts accounting-master audit stage.

Files changed:

- `backend/Garmetix.Api/FinalAccounts/FinalAccountsAccountingMasterAuditContracts.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsAccountingMasterAuditRules.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsAccountingMasterAuditService.cs`
- `backend/Garmetix.Api/FinalAccounts/FinalAccountsEndpoints.cs`
- `backend/Garmetix.Api/Program.cs`
- `backend/Garmetix.Api.Tests/FinalAccounts/FinalAccountsAccountingMasterAuditRulesTests.cs`
- `docs/final-accounts-bs-16-accounting-master-audit.md`
- `frontend/modular/scripts/final-accounts-readiness.mjs`
- `todo-balancesheet.md`, `.codex/todo.md`, `.claude/todo.md`

What changed:

- Added `GET /api/final-accounts/audit/accounting-master`.
- Registered `FinalAccountsAccountingMasterAuditService`.
- Audits existing Books accounting masters, parties, duplicates, source posting coverage and pending Final Accounts source-link coverage without writing data.
- Keeps BS-16 explicitly no-mutation: no migration, no schema repair, no backfill, no deploy and no service restart from this workstation.
- Documents the required deployed-host backup command before remote deploy/live audit:
  `npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit`.

Validation:

- `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release -p:UseSharedCompilation=false`: passed with the same 7 pre-existing nullable warnings outside Final Accounts.
- `dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release -p:UseSharedCompilation=false`: 231 passed, 3 pre-existing Postgres-only skipped, 0 failed.
- `npm --prefix frontend/modular run final-accounts:readiness`: passed.
- `npm --prefix frontend/modular run check`: passed.
- `git diff --check`: passed.

Remote status: backup file/history row and live audit output are pending on the deployed SRP host because local SSH/ping to `192.168.11.127` timed out.

## 2026-07-14 - Stage-Aware Database Backup Protocol

Amit instructed that every implementation stage/deploy capable of affecting database state must take a restore-ready backup first. Implemented the protocol in repo docs and SRP deploy tooling.

Files changed:

- `frontend/modular/deploy/srp-backup-database.sh`
  - Requires `--stage=<StageName>` for real backups.
  - Defaults backup directory to `/opt/garmetix/backup/database`.
  - Creates PostgreSQL custom-format dumps with date/time/stage/version in the filename.
  - Writes `.sha256` files.
  - Creates/appends `/opt/garmetix/backup/database/Backupfilehistory.md` with restore metadata.

- `frontend/modular/deploy/srp-whole-site-deploy.sh`
  - Adds `--stage=<StageName>`.
  - Runs a pre-deploy database backup before upload/install.
  - Allows `--skip-db-backup` only as an explicit override.

- `frontend/modular/deploy/srp-deploy.config.example.env`
  - Adds `SRP_BACKUP_DIR=/opt/garmetix/backup/database`.
  - Documents optional `SRP_DEPLOY_STAGE`.

- `frontend/modular/package.json`
  - Adds `deploy:srp:backup:list`.

- `docs/database-stage-backup-protocol.md`
  - Documents backup naming, history, restore and agent rules.

- `.codex/instructions.md`, `.codex/roadmap.md`, `.codex/todo.md`, `.codex/learnings.md`
  - Adds Codex standing rules, future accounting-unification stages and backup invariant.

- `.claude/*` and root instruction files
  - Mirrored the backup invariant for Claude Code and Codex.
