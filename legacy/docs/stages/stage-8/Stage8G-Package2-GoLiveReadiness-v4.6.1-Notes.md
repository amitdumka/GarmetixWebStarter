# Stage 8G Package 2 - Go-Live Readiness and Branding Restore (v4.6.1)

This package continues Stage 8G by focusing on production runbook readiness and restoring the earlier customer-provided brand assets.

## Included

- Restored the earlier Garmetix / Aadwika Fashion logo in the login/title experience and sidebar brand assets.
- Replaced generated placeholder app icon assets with the earlier provided logo set.
- Added Linux/WSL go-live readiness script that checks Docker, local health, app info, production readiness, and backup maintenance endpoints.
- Added backup/restore drill helper script for fresh backup creation and maintenance verification.
- Kept Mac mini deploy defaults safe with `RESET_DATABASE_ON_DEPLOY=false`.
