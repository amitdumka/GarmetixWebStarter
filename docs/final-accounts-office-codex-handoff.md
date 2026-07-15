# Final Accounts Office Codex Handoff

Date prepared: 2026-07-15

Branch to use:

```bash
version6
```

Latest known pushed commit at handoff:

```text
d424b5f chore(deploy): add restore drill safety gate
```

## Goal For Office Computer

Continue from local coding/tooling completion into remote SRP evidence capture.

The Balance Sheet / Final Accounts accounting-unification work is locally implemented through BS-21. The next work is not more local coding by default. The next work is:

1. pull latest `version6` on the office/SRP-capable computer;
2. deploy or run from the SRP network;
3. take stage backups;
4. capture read-only evidence endpoints;
5. run the restore drill;
6. stop before any live mutation until Amit/CA approval.

## Setup On Office Computer

If the repo already exists:

```bash
cd C:\AIarea\Codex\GarmetixWebStarter
git checkout version6
git pull origin version6
git status
```

If the repo is not present:

```bash
mkdir C:\AIarea\Codex
cd C:\AIarea\Codex
git clone https://github.com/amitdumka/GarmetixWebStarter.git
cd GarmetixWebStarter
git checkout version6
```

Run local sanity checks:

```bash
npm --prefix frontend/modular run final-accounts:readiness
npm --prefix frontend/modular run deploy:srp:restore-drill -- --dry-run
```

## SRP Config Needed

The deploy/backup/restore scripts read:

```text
~/.config/garmetix/srp-deploy.env
~/.config/garmetix/srp-deploy.secrets.env
```

Use the template:

```text
frontend/modular/deploy/srp-deploy.config.example.env
```

Do not commit passwords or secrets.

## Recommended Remote Sequence

Run from the office computer after it can reach the SRP host.

### 1. Pull Latest Code

```bash
git checkout version6
git pull origin version6
git status
```

### 2. Deploy With BS-20 Backup

This deploy command automatically takes the stage backup first:

```bash
npm --prefix frontend/modular run deploy:srp -- --stage=BS20FinalAccountsLedgerIntegration --install-remote
```

If deployment is not needed because SRP already has this commit, at minimum run:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS20FinalAccountsLedgerIntegration
```

### 3. Capture Read-Only Evidence Endpoints

Use a real authenticated Final Accounts-capable token/session. Suggested financial-year parameters:

```text
from=<fy-start>
to=<fy-end>
asOf=<fy-end>
```

Capture these:

```text
GET /api/final-accounts/audit/accounting-master
GET /api/final-accounts/audit/coa-normalization
GET /api/final-accounts/audit/party-ledger-unification
GET /api/final-accounts/audit/transaction-backfill-reconciliation?from=<fy-start>&to=<fy-end>&modules=Sales,Purchase,CashBank,Payroll,Gst,Inventory
GET /api/final-accounts/audit/direct-ledger-integration?from=<fy-start>&to=<fy-end>&asOf=<fy-end>
```

Save the outputs as evidence files outside git, or attach/summarize them in the office Codex task.

### 4. Run BS-21 Restore Drill

```bash
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
```

The restore drill should:

- verify `.sha256`;
- verify selected backup appears in `Backupfilehistory.md`;
- run `pg_restore -l`;
- restore into a non-production `garmetix_restore_drill_*` database;
- run SQL smoke;
- start temporary API against restored database;
- call `/api/health`;
- append `/opt/garmetix/backup/database/RestoreDrillHistory.md`.

### 5. Capture Restore Evidence

Capture:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
/opt/garmetix/backup/database/RestoreDrillHistory.md
```

At minimum record:

- backup filename;
- stage name;
- checksum result;
- restore DB name;
- SQL table count;
- `/api/health` result;
- whether restore DB was dropped or kept.

## Hard Stop Rules

Do not run any of these until Amit/CA approve the evidence:

- live COA normalization;
- ledger group relabel/relink;
- PartyId relink;
- party merge;
- ledger merge;
- live transaction backfill;
- Final Accounts report-source switch;
- production database restore;
- schema repair for accounting unification.

The current BS-16 through BS-20 endpoints are read-only evidence tools. BS-21 restore drill restores only into non-production.

## Useful Files To Read First

```text
.codex/todo.md
.codex/changelog.md
.codex/learnings.md
.claude/todo.md
todo-balancesheet.md
docs/database-stage-backup-protocol.md
docs/final-accounts-bs-16-accounting-master-audit.md
docs/final-accounts-bs-17-coa-normalization.md
docs/final-accounts-bs-18-party-ledger-unification.md
docs/final-accounts-bs-19-transaction-backfill-reconciliation.md
docs/final-accounts-bs-20-direct-ledger-integration.md
docs/final-accounts-bs-21-restore-drill-production-safety.md
```

## Copy/Paste Prompt For Office Codex

```text
Continue the Garmetix Final Accounts / Balance Sheet accounting-unification work from branch version6. First read docs/final-accounts-office-codex-handoff.md, .codex/todo.md, .codex/learnings.md, docs/database-stage-backup-protocol.md, and docs/final-accounts-bs-21-restore-drill-production-safety.md. Do not start live accounting mutation. Pull latest origin/version6, verify readiness, then run the SRP backup/deploy/evidence capture and BS-21 restore drill from this SRP-capable office computer. Capture evidence for BS-16 through BS-20 read-only endpoints and RestoreDrillHistory.md. Stop and summarize evidence for Amit/CA approval before any COA normalization, party/ledger relink, transaction backfill, report-source switch, or production restore.
```
