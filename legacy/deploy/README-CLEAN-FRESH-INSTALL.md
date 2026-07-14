# Garmetix Clean Fresh Install

This package is intended for a clean Mac mini production installation.

## What changed

- Old incremental EF Core migration files have been removed.
- One clean baseline migration remains: `20260623123000_InitialCreate`.
- Production Docker uses `DATABASE_SCHEMA_BOOTSTRAP_MODE=Migrate` for the new single `InitialCreate` migration.
- The API creates a clean PostgreSQL schema by applying the single `InitialCreate` EF migration after the old Docker volume is removed.

## Important warning

`RESET_DATABASE_ON_DEPLOY=false` is now the safe default and does not remove the PostgreSQL Docker volume.

Temporarily set `RESET_DATABASE_ON_DEPLOY=true` only when you intentionally want to wipe a test database, for example:

- a new server where no business data exists;
- a failed test deployment;
- a current install attempt where migrations failed before real data entry.

Do not enable it after live business data exists.

## Current package default

Create `deploy/macmini.env` locally from `deploy/macmini.env.example`; the private env file should have:

```bash
DATABASE_SCHEMA_BOOTSTRAP_MODE=Migrate
RESET_DATABASE_ON_DEPLOY=false
```

During WSL deployment, the script uploads the freshly generated `.env.production` to `/opt/garmetix/shared/env/.env.production`. Set `RESET_DATABASE_ON_DEPLOY=true` for one deploy only when you want to wipe the Mac mini PostgreSQL Docker volume and create a clean database from `InitialCreate`. The local flag is automatically returned to `false` after packaging.

## WSL deploy command

```bash
chmod +x deploy/*.sh 2>/dev/null || true
./deploy/deploy-to-macmini.sh
```

## Manual reset on Mac mini

```bash
cd /opt/garmetix/current
./deploy/reset-production-database.sh --yes
./deploy/run-production.sh
```
