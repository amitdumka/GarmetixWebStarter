# Stage 12E.5 - Books Petty Cash Review

Version: 5.12.19

## Scope

This stage replaces the modular Books petty cash placeholder with a read-only review workspace. It focuses on saved sheets, calculated daily summaries, mismatch visibility, and print-download readiness.

## Added

- Saved petty cash sheet list with store/date search.
- Store and date selector for calculated petty cash preparation.
- Latest cash-in-hand card based on the most recent saved sheet.
- Selected sheet detail table comparing saved values with calculated values.
- Mismatch table for fields that differ from calculated petty cash preparation.
- Calculation notes from the API preparation endpoint.
- Authenticated A5 PDF download button for saved petty cash sheets.
- Version marker and modular structure validation coverage.

## Connected Endpoints

- `stores`
- `petty-cash-sheets`
- `petty-cash-sheets/{id}`
- `petty-cash-sheets/prepare`
- `petty-cash-sheets/{id}/pdf`

## Safety

- No POST, PUT, DELETE, save, edit, alert, or repair endpoints are called.
- Owner mismatch alert delivery remains in the backend save flow and is not triggered by this review page.
- PDF downloads use the stored bearer token instead of opening an unauthenticated raw URL.

## How To Test

```bash
npm --prefix modular run build:books
```

Run locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open `http://localhost:3104/petty-cash` after logging in with an accounting-capable user.

## Next Step

Stage 12E.6 should connect vendor payment and settlement review in the Books app: purchase/vendor payment references, linked vouchers, bank/cash mode visibility, and PDF/download readiness where supported.
