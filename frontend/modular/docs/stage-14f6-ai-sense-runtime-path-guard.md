# Stage 14F.6 - AI Sense Runtime Path Guard

Version: 6.0.49

## What Changed

- Added AI Sense local API path normalization so page calls using `api/...` are stripped before they reach the shared API client.
- Added a modular workspace-link readiness and repair script to prevent stale copied `node_modules/@garmetix/*` packages from being bundled into app builds.
- Wired the workspace-link repair into SRP deployment before modular app builds.
- Fixed SRP deployment to run the repair script through the detected npm command, which supports WSL environments where `npm.cmd` exists but Linux `node` does not.
- Kept the previous shared API duplicate `/api` normalization as a second guard.

## Why

The source package was fixed in `6.0.47`, but the local install had stale copied `@garmetix` packages at `6.0.22`. Nuxt bundled the stale shared API client, so the live AI Sense app still requested `/api/api/...`.

## Validation

- `npm run modular:workspace-links -- --repair`
- `npm run modular:ai-sense:api-path`
- `npm --prefix frontend/modular --workspace @garmetix/ai-sense-web run build`
- `npm run modular:deploy:srp`
- Public browser check for `https://srp.aadwikafashion.in/ai-sense/`
