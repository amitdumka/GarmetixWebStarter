# Stage 8A Package 5 - Billing and Sales Return

Version: 4.0.3

Build code: `GARMETIX-8A-20260613-4003`

Date: 2026-06-13

## Implemented

- Standardized the Billing invoice register with `UiRegisterPanel`.
- Added retained sanitized load errors, retry, loading, filtered empty states, search, and invoice-status filtering.
- Kept the New Invoice action visible in both the page header and register toolbar.
- Preserved the extra-wide invoice workspace for customer selection, barcode/product entry, customer adjustments, split payments, and totals.
- Preserved invoice receipt, A4/A5/thermal printing, PDF download, return, exchange, cancellation, and stock/accounting behavior.
- Standardized the Sales Return register with `UiRegisterPanel`, `UTable`, search, retry, loading, and responsive overflow handling.
- Widened the Sales Return item workspace for practical invoice-line review.
- Added payment-mode selection and bank-account linkage for non-cash refunds.
- Added client validation preventing a non-cash refund without a bank account.
- Preserved item-wise stock reversal, return credit note, immediate refund, and customer store-credit behavior.
- Refreshed both modules when the working workspace changes.
- Migrated UI audit browser progress from v4.0.2 to v4.0.3 without discarding saved review notes.
- Marked Billing and Sales Return reviewed in the Stage 8A audit baseline.
- Synchronized frontend, backend, npm, package-lock, .NET package, assembly, file, release, and build-code identity to v4.0.3.

## Validation

```powershell
python scripts/validation/current-release-checks.py
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm.cmd run build
```

Browser verification covers `/billing`, `/sales-return`, both wide entry workspaces, mobile layouts, `/ui-audit`, and `/api/app-info/version`.
