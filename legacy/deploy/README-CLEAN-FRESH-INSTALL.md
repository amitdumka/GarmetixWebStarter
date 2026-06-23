# Garmetix Clean Fresh Install

This package is intended for a clean Mac mini production installation.

## What changed

- Old incremental EF Core migration files have been removed.
- One baseline migration marker remains: `20260617000000_InitialFreshSchema`.
- Production Docker uses `DATABASE_SCHEMA_BOOTSTRAP_MODE=FreshBaseline`.
- The API creates the schema from the current `GarmetixDbContext` model with `EnsureCreated()`, then marks the baseline migration as applied.

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
DATABASE_SCHEMA_BOOTSTRAP_MODE=FreshBaseline
RESET_DATABASE_ON_DEPLOY=false
```

During WSL deployment, the archive carries the safe `false` default to the Mac mini. Use `deploy/reset-production-database.sh --yes` for an intentional manual reset instead of leaving auto-reset enabled.

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
