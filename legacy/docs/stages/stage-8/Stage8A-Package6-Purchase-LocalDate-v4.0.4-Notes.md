# Stage 8A Package 6 - Purchase and Local Date Correction

Version: 4.0.4

Build code: `GARMETIX-8A-20260613-4004`

Date: 2026-06-13

## Implemented

- Fixed Petty Cash create/edit date serialization so the selected calendar date is saved without a UTC day shift.
- Changed the Petty Cash default date to use the browser's local calendar date.
- Preserved previous-day closing-balance lookup and transaction pre-calculation without changing their arithmetic.
- Changed Purchase and Purchase Return default dates to local calendar dates.
- Changed Purchase Return submission to preserve the selected local return date without UTC conversion.
- Standardized the Purchase register with retained sanitized errors, retry, loading, empty, search, and status-filter states.
- Added invoice date to the Purchase register.
- Preserved the extra-wide Purchase inward workspace, product lookup, GST, payment, receipt, PDF, and cancellation workflows.
- Standardized Purchase Return with `UiRegisterPanel`, `UTable`, search, retry, loading, and responsive table handling.
- Widened the item-wise Purchase Return workspace and replaced nested summary cards with a compact summary.
- Refreshed Purchase and Purchase Return when the working workspace changes.
- Migrated UI audit progress from v4.0.3 to v4.0.4 and marked both purchase routes reviewed.
- Synchronized frontend, backend, npm, package-lock, .NET package, assembly, file, release, and build-code identity to v4.0.4.

## Validation

```powershell
python scripts/validation/current-release-checks.py
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm.cmd run build
```

Browser verification covers Petty Cash selected dates, `/purchase`, `/purchase-return`, wide workspaces, mobile layouts, `/ui-audit`, and `/api/app-info/version`.
