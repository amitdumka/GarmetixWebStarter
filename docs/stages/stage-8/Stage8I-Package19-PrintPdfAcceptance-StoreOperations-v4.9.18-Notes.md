# Stage 8I Package 19 — Print/PDF Acceptance and Store Operations Landing v4.9.18

## Scope

Package 19 finalizes print/PDF acceptance coverage and makes Store Operations the first page for store-operational users.

## Backend

- Expanded `/api/print-acceptance/status` to include sales invoice, sales return, purchase return, debit/credit note, non-GST goods, salary payslip and salary payment samples.
- Kept existing voucher, cash voucher, petty cash, purchase inward, tailoring and GST export samples.
- Added dashboard home routing so Store Manager and Salesman/biller roles resolve to `/store-day` with dashboard type `StoreOperations`.
- Updated release smoke dashboard contract for Store Operations landing.

## Frontend

- Renamed Store Day Open / Close to **Store Operations** in the page title, module header, menu, and access-control route label.
- Updated `/dashboard` fallback routing and shell brand/home target so store manager and biller roles open `/store-day` first.
- Expanded Print Final Acceptance text, checklist and quick navigation for the full print/PDF handover set.

## Scripts and validation

- Added `scripts/linux/print-pdf-acceptance-drill.sh`.
- Added `scripts/validation/print-pdf-acceptance-check.py`.
- Added `PRINT_PDF_ACCEPTANCE` to the Test Automation manifest and smoke required set.
- Added `stage8i-package19-static-checks.py` and updated `current-release-checks.py`.

## Version

- Version: `4.9.18`
- Build: `GARMETIX-8I-20260619-49180`
