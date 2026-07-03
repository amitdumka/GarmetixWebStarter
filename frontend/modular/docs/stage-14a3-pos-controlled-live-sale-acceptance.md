# Stage 14A.3 POS Controlled Live Sale Acceptance

Version: `6.0.3`

This stage adds a controlled POS live sale acceptance runner for the deployed
SRP site. The default mode is read-only and non-mutating.

## Changes

- Added `frontend/modular/scripts/pos-live-sale-acceptance.mjs`.
- Added package scripts:
  - `modular:pos:live-sale-acceptance`
  - `pos:live-sale-acceptance`
- Bumped the visible modular stage to
  `Stage 14A.3 POS Controlled Live Sale Acceptance`.

## Safety Rule

The script does not create a sale invoice unless all of these are provided:

- `--live`
- `--mutate-sale`
- `--confirm-live-sale=YES`
- `--backup-file=<remote .dump backup path>`
- `GARMETIX_SMOKE_AUTH_TOKEN` or `--token-env=<token env name>`

Read-only live mode checks deployed POS shell, Nuxt assets, API health and auth
gate. When a token is present it also verifies store access, billing options,
default Manager salesman readiness, sellable product lookup and sale payload
shape without posting.

## Commands

Read-only dry run:

```bash
npm run modular:pos:live-sale-acceptance
```

Read-only live SRP check:

```bash
npm run modular:pos:live-sale-acceptance -- --live
```

Controlled live sale acceptance after a fresh backup:

```bash
npm run modular:pos:live-sale-acceptance -- --live --mutate-sale --confirm-live-sale=YES --backup-file=/opt/garmetix-srp/backups/<backup-file>.dump
```

Optional selectors:

```bash
--barcode=<known-test-barcode>
--query=<product-search-text>
--store-id=<store-guid>
```

## Next

After one controlled sale save and PDF/receipt verification passes, continue
with POS return acceptance, exchange acceptance, held bills, day open/day close
and print queue recovery.
