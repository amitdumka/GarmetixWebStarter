# Stage 8A Package 4 - Loyalty and Petty Cash

Version: 4.0.2

Build code: `GARMETIX-8A-20260613-4002`

Date: 2026-06-13

## Implemented

- Added retryable store-level loading errors to Loyalty setup.
- Added an independent customer-loyalty ledger loading and error state.
- Standardized the Loyalty ledger with `UiRegisterPanel`, search, aligned values, empty states, and responsive controls.
- Replaced nested loyalty summary cards with a compact responsive summary grid.
- Standardized the Petty Cash register with `UiRegisterPanel`.
- Added sanitized retained errors, retry, search, loading, and empty states to the daily cash register.
- Widened the Petty Cash entry workspace for practical review of daily cash-in and cash-out fields.
- Preserved automatic previous-closing carry-forward and transaction-based pre-calculation.
- Preserved automatic cash-in-hand calculation, reconciliation differences, Message Log alerts, and automatic print after save.
- Preserved the isolated colored A5 landscape two-section print document.
- Migrated UI audit browser progress from v4.0.1 to v4.0.2 without discarding saved review notes.
- Marked Loyalty and Petty Cash reviewed in the Stage 8A audit baseline.
- Synchronized frontend, backend, npm, package-lock, .NET package, assembly, file, release, and build-code identity to v4.0.2.

## Validation

```powershell
python scripts/validation/current-release-checks.py
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm.cmd run build
```

Browser verification covers `/loyalty`, `/petty-cash`, the wide Petty Cash form, mobile layouts, `/ui-audit`, and `/api/app-info/version`.
