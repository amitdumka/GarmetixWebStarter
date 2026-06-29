# Stage 13B.9 POS Held Bill Smoke

Version: 5.13.14
Branch: Version5

## Scope

This stage adds repeatable smoke coverage for the server-backed POS held-bill API introduced in Stage 13B.8. It does not change API behavior or database schema.

## Added

- Script: `modular/scripts/pos-held-bill-smoke.mjs`
- Root command: `npm.cmd run modular:pos:held-bill-smoke`
- Modular command: `npm.cmd --prefix modular run pos:held-bill-smoke`
- Validation coverage in `modular/scripts/validate-all.mjs`

## Dry-Run Behavior

The default command is non-mutating and does not require API/server access:

```powershell
npm.cmd run modular:pos:held-bill-smoke
```

It prints the expected checks for:

- unauthenticated held-bill list auth gate
- authenticated held-bill list
- optional held-bill create/delete lifecycle

## Live Auth Gate Check

Run against local or public API:

```powershell
npm.cmd run modular:pos:held-bill-smoke -- --live
npm.cmd run modular:pos:held-bill-smoke -- --mode=public --live
```

When `GARMETIX_SMOKE_AUTH_TOKEN` is set, the script also checks authenticated list access:

Set the token in your shell or host secret store first, then run:

```powershell
npm.cmd run modular:pos:held-bill-smoke -- --live --require-token
```

## Optional Mutation Check

Only run this against a disposable test workspace. It creates one smoke held bill and immediately deletes it:

```powershell
npm.cmd run modular:pos:held-bill-smoke -- --live --mutate --company-id=<guid> --store-group-id=<guid> --store-id=<guid>
```

## Safety Rules

- No usernames, passwords, tokens or private host credentials are stored in source.
- Mutation is opt-in through `--mutate`.
- Mutation requires a bearer token plus company/store ids.
- Created smoke records use a `smoke-` client id and are deleted during the same run.

## Validation

- `npm.cmd run modular:pos:held-bill-smoke`
- `npm.cmd run modular:check`
- `npm.cmd run modular:deploy:preflight`
- `npm.cmd run modular:validate -- --skip-builds`

## Remaining Follow-Up

- Run live mutation smoke after test credentials and workspace ids are available.
- Add Playwright browser coverage for hold, resume and save-after-resume when local POS/API test fixtures are ready.
