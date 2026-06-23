# Stage 12A.5 Shared Shell Status

Stage 12A.5 adds the first reusable runtime shell contracts for the Version5 modular apps.

## What Changed

- `@garmetix/shared-api` now exposes API health helpers:
  - `createApiHealthUrl`
  - `checkApiHealth`
  - sanitized API error messages through `stripServerUrl`
- `@garmetix/shared-auth` now exposes browser token snapshot helpers:
  - `setStoredToken`
  - `clearStoredToken`
  - `getAuthSessionSnapshot`
- `@garmetix/shared-ui` now exposes shell status card contracts:
  - `ShellStatusInput`
  - `ShellStatusCard`
  - `buildShellStatusCards`
- All six modular shell apps now show consistent status cards:
  - owned route count
  - API service health
  - auth token state
  - current stage

## Important Notes

- This stage does not add real login screens yet.
- This stage does not move any legacy route implementation.
- API health uses the configured public API base URL plus `/health`, so `http://localhost:5080/api` becomes `http://localhost:5080/api/health`.
- Error text shown by shared API helpers is sanitized so server URLs are not exposed in UI messages.

## Next Step

Stage 12B should start POS extraction with real pages under `modular/apps/pos`, beginning with login/session wiring and the counter dashboard shell.
