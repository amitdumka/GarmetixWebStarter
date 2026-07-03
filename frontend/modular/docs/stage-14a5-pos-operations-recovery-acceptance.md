# Stage 14A.5 POS Operations Recovery Acceptance

Version: 6.0.5

## Purpose

Stage 14A.5 adds a repeatable POS operations recovery check for the SRP modular deployment. It verifies the operational screens and backend contracts that keep the counter usable when a cashier needs to resume a bill, recover a print, open or close the store day, or hand work to the DotMatrix print queue.

## Covered Areas

- POS held bill recovery page: `/pos/hold-bills`
- POS print recovery page: `/pos/print`
- POS day open page: `/pos/day-open`
- POS day close page: `/pos/day-close`
- API health and auth gates
- Server held-bill listing
- Recent invoice listing for print recovery
- Store day status for day open/day close
- DotMatrix queue stats, settings and failed queue visibility when the token has accounting/admin permission

## Safety Rules

The script is read-only by default. It does not open or close store days, it does not create a sale, it does not reprint an invoice, and it does not send anything to the physical printer unless explicitly requested.

Optional mutations are separately guarded:

- Held bill create/delete requires `--live --mutate-held-bill --confirm-held-bill=YES --backup-file=<remote .dump>`.
- DotMatrix test print queueing requires `--live --queue-dotmatrix-test --confirm-dotmatrix-test=YES --backup-file=<remote .dump>`.

Both mutation flows require a token and a fresh backup file. Store-day open/close mutation is intentionally not included in this script because it can affect live store-day accounting.

## Commands

Dry run:

```powershell
npm.cmd run modular:pos:operations-recovery
```

Live read-only SRP check:

```powershell
$env:NODE_OPTIONS='--use-system-ca'
npm.cmd run modular:pos:operations-recovery -- --live
```

Optional guarded held-bill create/delete:

```powershell
$env:GARMETIX_SMOKE_AUTH_TOKEN='<token>'
npm.cmd run modular:pos:operations-recovery -- --live --mutate-held-bill --confirm-held-bill=YES --backup-file=/opt/garmetix-srp/backups/<backup>.dump
```

Optional guarded DotMatrix test print queue:

```powershell
$env:GARMETIX_SMOKE_AUTH_TOKEN='<accounting-or-admin-token>'
npm.cmd run modular:pos:operations-recovery -- --live --queue-dotmatrix-test --confirm-dotmatrix-test=YES --backup-file=/opt/garmetix-srp/backups/<backup>.dump
```

## Acceptance Notes

- Without `GARMETIX_SMOKE_AUTH_TOKEN`, the live run still verifies public POS routes, API health, and unauthenticated auth gates.
- With a POS token, it verifies held bills, recent invoices and store-day status.
- With an accounting/admin-capable token, it also verifies DotMatrix queue readiness.
- DotMatrix physical printer alignment remains an operator/device acceptance item.

## Next Stage

Stage 14A.6 should move from operational readiness into cashier workflow parity: product scan edge cases, payment split confirmation, return/exchange cashier flow polish, and browser acceptance for 14-inch laptop use.
