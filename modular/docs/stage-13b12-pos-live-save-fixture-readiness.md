# Stage 13B.12 POS Live Save Fixture Readiness

Version: 5.13.17
Branch: Version5

## Scope

This stage prepares POS live save-and-print acceptance without creating real invoices. It checks whether the selected API/user/store has the minimum safe fixtures needed before a future opt-in mutation test runs.

## Added

- Script: `modular/scripts/pos-live-save-fixture-readiness.mjs`
- Root command: `npm.cmd run modular:pos:live-save-fixtures`
- Modular command: `npm.cmd --prefix modular run pos:live-save-fixtures`
- Validation coverage in `modular/scripts/validate-all.mjs`

## Dry-Run Command

```powershell
npm.cmd run modular:pos:live-save-fixtures
```

Dry run prints the expected checks and does not require API access, database access, Playwright, or credentials.

## Optional Live Commands

Set a smoke bearer token in the shell or host secret store, then run:

```powershell
npm.cmd run modular:pos:live-save-fixtures -- --live --require-token
npm.cmd run modular:pos:live-save-fixtures -- --mode=public --live --require-token
```

Useful filters:

```powershell
npm.cmd run modular:pos:live-save-fixtures -- --live --store-id=<guid>
npm.cmd run modular:pos:live-save-fixtures -- --live --barcode=<barcode>
npm.cmd run modular:pos:live-save-fixtures -- --live --query=shirt
npm.cmd run modular:pos:live-save-fixtures -- --live --require-bank
```

## Live Checks

The live readiness script verifies:

- API health endpoint responds.
- Token can call `/api/auth/me`.
- `/api/stores` returns at least one permitted store.
- `/api/billing/options` responds for the selected store.
- Billing options include salesman rows when available, while keeping backend Manager fallback documented.
- `/api/product-lookup` finds at least one non-OFB stock row with positive available quantity.
- `/api/bank-accounts` can support non-cash acceptance when required.
- A safe `billing/sales` payload can be constructed from the selected fixture.

## Safety Rules

- No usernames, passwords, bearer tokens, tunnel tokens or host credentials are stored in source.
- This stage never posts `billing/sales`.
- Real invoice creation remains a future opt-in stage.
- Use disposable test products, stock and store data before enabling any mutation acceptance.

## Validation

- `npm.cmd run modular:pos:live-save-fixtures`
- `npm.cmd run modular:check`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Add a fully opt-in `--mutate` save-and-print acceptance after the fixture readiness check passes against a disposable store.
- Store live acceptance evidence outside source control and redact customer, salary, token and host details.
