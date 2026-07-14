# Stage 14F.5 - AI Sense API Path Hotfix

Version: 6.0.47

## What Changed

- Normalized shared modular API URL building so a base API URL ending in `/api` does not duplicate a page path that also starts with `api/`.
- Fixed the AI Sense public failure pattern where requests became `/api/api/dashboard/business` and `/api/api/inventory/stock-reports/summary`.
- Kept the fix inside `@garmetix/shared-api` so POS, HR, Books, CRM, Admin and Main also benefit from the same guard.
- Added a regression readiness script for the exact AI Sense dashboard and stock summary paths.

## Safety Rules

- No database change.
- No backend behavior change.
- No route deletion.
- The fix only changes frontend request URL composition before `fetch`.

## Validation

- `npm run modular:ai-sense:api-path`
- `npm --prefix frontend/modular run validate -- --skip-builds --skip-api`
- `npm --prefix frontend/modular --workspace @garmetix/ai-sense-web run build`
- `npm run modular:deploy:srp`
- `npm run modular:deploy:srp:acceptance -- --live`
