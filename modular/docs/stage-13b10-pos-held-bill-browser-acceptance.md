# Stage 13B.10 POS Held Bill Browser Acceptance

Version: 5.13.15
Branch: Version5

## Scope

This stage adds repeatable browser acceptance coverage for the POS held-bill workflow. It does not change API behavior, database schema, production credentials, or the POS pages themselves.

## Added

- Script: `modular/scripts/pos-held-bill-browser-acceptance.mjs`
- Root command: `npm.cmd run modular:pos:held-bill-browser`
- Modular command: `npm.cmd --prefix modular run pos:held-bill-browser`
- Validation coverage in `modular/scripts/validate-all.mjs`

## Dry-Run Behavior

The default command is non-mutating and does not require the POS app, API, database, Playwright, or credentials:

```powershell
npm.cmd run modular:pos:held-bill-browser
```

It prints the expected browser acceptance steps for:

- opening POS Hold Bills with a seeded browser-local held bill
- verifying the held bill card renders
- clicking Resume
- verifying navigation reaches Sale
- verifying sale-draft recovery
- verifying the original local held bill is removed

## Optional Live Browser Check

Run this after the POS app is running locally or deployed publicly. The live check uses a seeded browser-local held bill and a non-secret UI token when a real smoke token is not present:

```powershell
npm.cmd run modular:pos:held-bill-browser -- --live
npm.cmd run modular:pos:held-bill-browser -- --mode=public --live
```

When a real bearer token is available, set `GARMETIX_SMOKE_AUTH_TOKEN` in the shell or host secret store before running. The script will use it without storing it in source:

```powershell
npm.cmd run modular:pos:held-bill-browser -- --live --token-env=GARMETIX_SMOKE_AUTH_TOKEN
```

## Live Acceptance Checks

At 1366x768 laptop viewport, the live script:

- seeds a temporary browser session
- seeds one local held bill under `garmetix.pos.held-bills.v1`
- opens `/hold-bills`
- verifies `Hold Bills`, the smoke customer and `Resume` are visible
- clicks Resume
- verifies navigation to `/sale`
- verifies `garmetix.pos.sale.draft.v1` contains the restored held bill
- verifies the original held bill is removed from local held-bill storage

## Safety Rules

- No usernames, passwords, tokens, tunnel tokens or host credentials are stored in source.
- Dry run remains the default validation path.
- The seeded live record is browser-local unless the operator also runs server/API smoke scripts.
- The seeded product and ids are disposable smoke values and are not used to save an invoice.
- Use a disposable test workspace for any later full save-and-print acceptance.

## Validation

- `npm.cmd run modular:pos:held-bill-browser`
- `npm.cmd run modular:check`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Run the live browser acceptance when the POS dev server is active and Playwright is installed.
- Add a full live save-after-resume acceptance only after test product, store, salesman and payment fixtures are stable.
