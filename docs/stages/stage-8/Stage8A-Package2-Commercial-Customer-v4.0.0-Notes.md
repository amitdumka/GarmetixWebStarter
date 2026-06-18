# Stage 8A Package 2 - Commercial and Customer Registers

Version: 4.0.0

Build code: `GARMETIX-8A-20260613-4000`

Date: 2026-06-13

## Implemented

- Standardized the Commercial Notes register using `UiRegisterPanel`.
- Added note search, debit/credit filtering, loading, error/retry, empty states, aligned values, and responsive actions.
- Restored the visible New Credit Note action that the custom header slot previously replaced.
- Standardized the Customer register and customer loyalty ledger.
- Added customer loading, error/retry, empty, search, close-ledger, and loyalty-ledger states.
- Restored the visible New Customer action alongside Loyalty Setup.
- Marked Commercial Notes and Customers reviewed in the Stage 8A audit baseline.
- Versioned the browser audit baseline for v4.0.0 so existing Package 1 cache does not leave newly completed routes marked pending.
- Synchronized frontend, backend, npm, package-lock, .NET package, assembly, file, release, and build-code identity to v4.0.0.
- Added `current-release-checks.py` as the current aggregate static validation command.
- Fixed the dashboard home route handler so ASP.NET writes its DTO instead of discarding the response body.
- Resolved the purchase-receipt unit and data-consistency duplicate-number nullable warnings.

## Validation

```powershell
python scripts/validation/current-release-checks.py
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm.cmd run build
```

## Git

Stage 8A Packages 1 and 2 are intended to be committed together as the v4.0.0 UI audit foundation.
