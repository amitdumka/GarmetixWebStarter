# Stage 8A Package 3 - Party and Voucher Registers

Version: 4.0.1

Build code: `GARMETIX-8A-20260613-4001`

Date: 2026-06-13

## Implemented

- Standardized the combined Customer/Vendor Party register with `UiRegisterPanel`.
- Added retained, sanitized loading errors with an in-register retry action.
- Kept New Customer and New Vendor actions visible together in the page header.
- Preserved Party search, GST status, mismatch alerts, and the existing wide create form.
- Standardized the Accounting Voucher register with `UiRegisterPanel`.
- Added Payment, Receipt, and Expense filtering alongside voucher search.
- Preserved voucher posting, editing, deletion, PDF, banking, ledger, party, and employee behavior.
- Migrated UI audit browser progress from v4.0.0 to v4.0.1 without discarding saved notes.
- Marked Parties and Vouchers reviewed in the Stage 8A audit baseline.
- Synchronized frontend, backend, npm, package-lock, .NET package, assembly, file, release, and build-code identity to v4.0.1.

## Validation

```powershell
python scripts/validation/current-release-checks.py
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj
cd frontend/garmetix-web
npm.cmd run build
```

Browser verification covers `/parties`, `/vouchers`, and `/api/app-info/version` at desktop and mobile widths.
