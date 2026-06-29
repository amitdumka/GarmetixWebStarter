# Stage 13B.7 POS Operator Acceptance

Version: 5.13.12
Branch: Version5

## Scope

This stage adds a repeatable POS operator acceptance checklist for the modular POS app. It does not change the shared ASP.NET Core API, PostgreSQL schema, accounting behavior, sale posting, return posting or exchange posting.

## Added

- `modular/scripts/pos-operator-acceptance.mjs`
- Root script: `npm.cmd run modular:pos:operator-acceptance`
- Modular script: `npm.cmd --prefix modular run pos:operator-acceptance`
- Validation coverage in `modular/scripts/validate-all.mjs`

## Acceptance Focus

- 14 inch laptop viewport: `1366x768`
- 100 percent browser zoom
- Sale, Return and Exchange counter flows
- Day open and day close print recovery
- Held bill resume/delete recovery
- Print Queue retry after browser popup or PDF opening failure
- Clean user-facing errors without raw server URLs
- Bank account validation for non-cash payment, refund and exchange difference

## How To Run

Print the checklist to the terminal:

```powershell
npm.cmd run modular:pos:operator-acceptance
```

Write a dated checklist file under `modular/docs/generated`:

```powershell
npm.cmd run modular:pos:operator-acceptance -- --write
```

Use public URLs instead of local URLs:

```powershell
npm.cmd run modular:pos:operator-acceptance -- --mode=public
```

## Manual Test Data Needed

- One active cashier or store operator login.
- One openable store day.
- Products with available stock and barcode values.
- One recent sale invoice for return testing.
- One returnable invoice and one replacement product for exchange testing.
- One configured bank account for non-cash payment, refund and exchange difference checks.

## Validation

- `npm.cmd run modular:pos:operator-acceptance`
- `npm.cmd run modular:check`
- `npm.cmd run modular:deploy:preflight`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Server-backed held bill persistence.
- Live browser acceptance once local POS, API and test credentials are available.
- Visual refinements discovered during real cashier testing.
