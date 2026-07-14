# Stage 14A.4 POS Controlled Return And Exchange Acceptance

Version: `6.0.4`

This stage adds controlled live acceptance for POS sale return and exchange
workflows on the deployed SRP site. The default mode is read-only.

## Changes

- Added `frontend/modular/scripts/pos-live-return-exchange-acceptance.mjs`.
- Added package scripts:
  - `modular:pos:live-return-exchange-acceptance`
  - `pos:live-return-exchange-acceptance`
- Bumped the visible modular stage to
  `Stage 14A.4 POS Controlled Return And Exchange Acceptance`.

## Safety Rule

Return and exchange creation are disabled unless guarded mutation flags are
provided. Return and exchange mutations must be run separately.

Return mutation requires:

```bash
--live --mutate-return --confirm-live-return=YES --backup-file=/opt/garmetix-srp/backups/<backup>.dump
```

Exchange mutation requires:

```bash
--live --mutate-exchange --confirm-live-exchange=YES --backup-file=/opt/garmetix-srp/backups/<backup>.dump
```

Both require `GARMETIX_SMOKE_AUTH_TOKEN` or `--token-env=<token env name>`.

## Commands

Read-only dry run:

```bash
npm run modular:pos:live-return-exchange-acceptance
```

Read-only live SRP check:

```bash
npm run modular:pos:live-return-exchange-acceptance -- --live
```

Controlled live return:

```bash
npm run modular:pos:live-return-exchange-acceptance -- --live --mutate-return --confirm-live-return=YES --backup-file=/opt/garmetix-srp/backups/<backup>.dump --invoice-id=<test-invoice-guid>
```

Controlled live exchange:

```bash
npm run modular:pos:live-return-exchange-acceptance -- --live --mutate-exchange --confirm-live-exchange=YES --backup-file=/opt/garmetix-srp/backups/<backup>.dump --invoice-id=<test-invoice-guid> --replacement-barcode=<test-barcode>
```

## Next

Stage 14A.5 should continue POS parity with held bills, print queue recovery,
day open/day close and DotMatrix handoff.
