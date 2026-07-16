# Database Stage Backup Protocol

This protocol applies to Codex, Claude Code and any human running Garmetix deployment or database-changing implementation work.

## Rule

Before any implementation stage, deployment, migration, schema repair, bulk backfill, ledger migration, accounting unification, data correction or production service restart that can affect the PostgreSQL database, create a restore-ready backup on the deployed host.

Backups must be stored on the deployed system at:

```text
/opt/garmetix/backup/database/
```

The backup history file must live in the same folder:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
```

## Naming

Every backup filename must include:

- date;
- time;
- implementation stage name;
- app version.

Example:

```text
garmetix-srp-db-20260714-104512-IST-BS16AccountingMasterAudit-v6.0.51.dump
garmetix-srp-db-20260714-104512-IST-BS16AccountingMasterAudit-v6.0.51.dump.sha256
```

Stage names must be short and filesystem-safe. Use names like:

```text
BS16AccountingMasterAudit
BS17IndianCOANormalization
BS18PartyLedgerUnification
GST11EinvoiceLiveProvider
HRPayrollHotfix
```

## Standard Commands

Create a stage backup:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS16AccountingMasterAudit
```

List latest backups and history:

```bash
npm --prefix frontend/modular run deploy:srp:backup:list
```

Run a non-production restore drill:

```bash
npm --prefix frontend/modular run deploy:srp:restore-drill -- --confirm-non-production-restore --stage=BS21RestoreDrillProductionSafety
```

Deploy with automatic pre-deploy backup:

```bash
npm --prefix frontend/modular run deploy:srp -- --stage=BS16AccountingMasterAudit --install-remote
```

The deploy script refuses a real upload/install if no stage name is supplied. `--skip-db-backup` exists only for an explicitly approved emergency/manual exception.

## What The Script Does

`frontend/modular/deploy/srp-backup-database.sh`:

- SSHes to the SRP host using the normal SRP deploy config.
- Reads the database connection string from the remote API env file.
- Creates `/opt/garmetix/backup/database/` if missing.
- Runs `pg_dump -Fc`.
- Writes a `.sha256` checksum file.
- Creates or appends `Backupfilehistory.md`.
- Prints a restore-check hint.

## Swalekha (`swalekha_db`) - a second database, not yet covered by the standard command

The Swalekha (Personal & Personal Finance) module (`docs/personal-finance-module-design.md`) deliberately runs on its own separate PostgreSQL database, `swalekha_db`, alongside the main `garmetix` database on the same host. As of `PersonalFin_01` (Foundation), `swalekha_db` holds no domain data yet (its `SwalekhaDbContext` has zero tables) - `frontend/modular/deploy/srp-backup-database.sh` and the `deploy:srp:backup` command above **still only back up the main `garmetix` database**, not `swalekha_db`.

**Before any `PersonalFin_NN` stage (from `PersonalFin_02` onward) that adds real Swalekha tables or writes real data, the backup tooling must be extended to also dump `swalekha_db`** (its own `pg_dump -Fc`, its own checksum, its own `Backupfilehistory.md` row - reusing the same file/naming convention above, just naming the database explicitly, e.g. `swalekha-srp-db-<timestamp>-<Stage>-v<version>.dump`) before that stage's mutation runs. Do not assume the existing single-database backup command covers Swalekha - it does not, until this extension is built.

## Restore Guidance

Always restore into a separate database first unless Amit explicitly approves replacing the live database.

For normal restore proof, use the scripted non-production drill first. It verifies the checksum, reads the dump catalog, restores into a `garmetix_restore_drill_*` database, runs SQL smoke, starts the API temporarily against the restored database and records `/opt/garmetix/backup/database/RestoreDrillHistory.md`.

Template:

```bash
sha256sum -c garmetix-srp-db-YYYYMMDD-HHMMSS-IST-Stage-vX.dump.sha256
createdb -h <host> -p <port> -U <user> <restore_db>
pg_restore -h <host> -p <port> -U <user> -d <restore_db> --clean --if-exists --no-owner --no-privileges garmetix-srp-db-YYYYMMDD-HHMMSS-IST-Stage-vX.dump
```

Do not run destructive restore against production without explicit human confirmation naming the target host and backup file.

## Agent Instructions

Codex and Claude Code must:

- read this protocol before database-changing work;
- create the backup before implementation/deploy starts;
- record the backup filename in the stage TODO/changelog;
- never rely only on EF migrations as the restore plan;
- not delete old backups unless a separate retention policy is approved;
- keep production secrets out of repository files.

## Related Roadmap

The accounting unification stages must use this protocol because they will touch existing Sale, Purchase, Voucher, Salary Payment, Customer, Vendor, Party, Ledger and Ledger Group data.
