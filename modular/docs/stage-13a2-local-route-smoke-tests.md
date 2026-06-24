# Stage 13A.2 - Local Route Smoke Tests

Version: 5.13.2

## Goal

Add a repeatable route smoke test layer for the modular apps without changing frontend behavior, the shared ASP.NET API, or PostgreSQL.

## Added Commands

From the repository root:

```powershell
npm.cmd run modular:smoke:routes
npm.cmd run modular:smoke:routes -- --app=pos
npm.cmd run modular:smoke:routes -- --app=hr --live
```

From the `modular/` folder:

```powershell
npm.cmd run smoke:routes
```

## Modes

Dry mode is the default. It validates the route matrix and prints every URL that should be checked.

Live mode uses Playwright when it is installed and the local app servers are running. It visits the selected routes, checks that the page is not empty, records HTTP 500-level responses, records page runtime errors, and can fail on console errors with `--strict-console`.

Live mode is intentionally optional because local dev/static servers and browser binaries may not be available on every machine.

## Live Mode Setup

When live browser checks are needed, install Playwright in `modular/`:

```powershell
npm.cmd install --save-dev playwright
npx.cmd playwright install chromium
```

Then start the required API and app servers before running:

```powershell
npm.cmd run modular:smoke:routes -- --live
```

## App Ports

| App | Local URL |
| --- | --- |
| Main Back Office | `http://localhost:3100` |
| POS | `http://localhost:3101` |
| HR | `http://localhost:3102` |
| AI Sense | `http://localhost:3103` |
| Books | `http://localhost:3104` |
| Admin/SaaS | `http://localhost:3105` |
| API Health | `http://localhost:5080/api/health` |

## Acceptance

- Dry route smoke validation works for all apps and app-specific filters.
- The checklist and route smoke runner share the same route source.
- `modular:check` includes the new smoke files.
- No secrets or production credentials are added.

## Next Step

Stage 13A.3 should add API health and auth-state smoke checks for local and public URL modes.
