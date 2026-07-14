# Codex Changelog

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
