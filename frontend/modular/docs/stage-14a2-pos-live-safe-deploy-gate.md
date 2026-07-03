# Stage 14A.2 POS Live-Safe Deploy Gate

Version: `6.0.2`

This stage prepares POS for controlled live acceptance and SRP deployment
without creating invoices or touching live transaction data.

## Changes

- Added a `.127` SRP database backup script:
  `frontend/modular/deploy/srp-backup-database.sh`.
- Added `modular:deploy:srp:backup` and `deploy:srp:backup` package scripts.
- Added a POS live-safe deploy gate:
  `frontend/modular/scripts/pos-live-safe-deploy-gate.mjs`.
- Added `modular:pos:live-safe-gate` and `pos:live-safe-gate` package scripts.
- Bumped the visible modular stage to `Stage 14A.2 POS Live-Safe Deploy Gate`.

## Safety Rule

The Stage 14A.2 gate is non-mutating. It does not create, edit, delete or post
any sale invoice, return, exchange, store-day or cash record.

Before any controlled live POS write acceptance:

1. Run the SRP backup script without `--dry-run`.
2. Keep the generated backup file name in the test evidence.
3. Use a known test item and customer where possible.
4. Keep legacy frontend available as fallback.

## Commands

Dry safety gate:

```bash
npm run modular:pos:live-safe-gate
```

Gate plus POS build:

```bash
npm run modular:pos:live-safe-gate -- --with-build
```

Backup the `.127` SRP database:

```bash
npm run modular:deploy:srp:backup
```

List recent `.127` SRP backups:

```bash
npm run modular:deploy:srp:backup -- --list
```

Deploy after validation:

```bash
npm run modular:deploy:srp
npm run modular:deploy:srp:acceptance -- --live --strict
```

## Next

Stage 14A.3 should run controlled POS browser acceptance on `.127`:
sale save and print, return, exchange, held bills, day open/day close and print
queue recovery. Only then should HR parity start.
