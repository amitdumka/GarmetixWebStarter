# Stage 13A.3 - API And Auth Smoke Checks

Version: 5.13.3

## Goal

Add repeatable API health and auth-state smoke checks for the modular split while keeping one ASP.NET Core API, one PostgreSQL database and no committed credentials.

## Added Commands

From the repository root:

```powershell
npm.cmd run modular:smoke:api
npm.cmd run modular:smoke:api -- --mode=public
npm.cmd run modular:smoke:api -- --live
npm.cmd run modular:smoke:api -- --live --require-token
```

From the `modular/` folder:

```powershell
npm.cmd run smoke:api
```

## Dry Mode

Dry mode is the default and does not contact the API. It prints:

- The health endpoint for the selected mode.
- The `/api/auth/me` auth-gate check.
- The optional bearer-token check.
- The login endpoint contract without sending credentials.

This dry check is included in `npm.cmd run modular:validate`.

## Live Mode

Live mode performs safe GET requests only:

- `GET /api/health` must return `200`.
- `GET /api/auth/me` without a token must return `401` or `403`.
- If `GARMETIX_SMOKE_AUTH_TOKEN` is set, `GET /api/auth/me` with that token must return `200`.

The script never sends usernames or passwords. Use a short-lived token from a local login session when token verification is needed:

```powershell
[Environment]::SetEnvironmentVariable("GARMETIX_SMOKE_AUTH_TOKEN", "<short-lived-token>", "Process")
npm.cmd run modular:smoke:api -- --live --require-token
```

## Modes

| Mode | Health URL |
| --- | --- |
| Local | `http://localhost:5080/api/health` |
| Public | `https://api.garmetix.aadwikafashion.in/api/health` |

## Acceptance

- Dry API/auth smoke validation works in local and public modes.
- Live mode is available without storing secrets.
- `modular:validate` includes the dry API/auth check.
- No backend route, database or credential behavior is changed.

## Next Step

Stage 13A.4 should add public URL smoke reporting around Cloudflare Tunnel readiness and selected app/API reachability.
