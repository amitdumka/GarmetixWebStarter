# Stage 8I Package 15 - Docker Acceptance Drill (v4.9.14)

## Release identity

Version: 4.9.14

Stage: Stage 8I Package 15 Docker Acceptance Drill

Build code: GARMETIX-8I-20260619-49140

## Changes

- Added Linux and Windows Docker acceptance drill scripts.
- Added frontend route-access audit for concrete Nuxt pages.
- Added `FRONTEND_ROUTE_ACCESS_AUDIT` and `DOCKER_ACCEPTANCE_DRILL` to Test Automation manifest.
- Updated runtime smoke contract and smoke scripts to require the new manifest entries.
- Updated version metadata across backend, frontend, README, docs and smoke defaults.

## Validation

Run locally before ZIP handoff:

```bash
python3 scripts/validation/current-release-checks.py
```

Run on the production/deployment host:

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/docker-acceptance-drill.sh .env.production
```

## Acceptance note

This package adds the drill and static coverage. The actual Docker/fresh database/backup-restore drill remains open until executed on the target host with real credentials.
