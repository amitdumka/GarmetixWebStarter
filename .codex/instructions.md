# Codex Standing Instructions

This file is Codex's repo-local standing instruction layer. Root `AGENTS.md` is the short pointer; this file keeps durable process rules that should survive chat compaction.

## Database Backup Invariant

Before any implementation stage, deployment, migration, schema repair, backfill, accounting unification, ledger migration, production service restart or data correction that can affect PostgreSQL, create a named restore-ready database backup on the deployed host.

Use:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit
```

Backups and history must live on the SRP/deployed host at:

```text
/opt/garmetix/backup/database/
/opt/garmetix/backup/database/Backupfilehistory.md
```

Filenames must include date, time and stage name, for example:

```text
garmetix-srp-db-20260714-104512-IST-BS16AccountingMasterAudit-v6.0.51.dump
```

Full protocol: `docs/database-stage-backup-protocol.md`.

Do not skip this backup unless Amit explicitly approves the exception and the reason is recorded in the stage TODO/changelog.
