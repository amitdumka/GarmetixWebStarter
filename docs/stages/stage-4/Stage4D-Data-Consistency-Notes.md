# Stage 4D — Test Coverage and Data Consistency Verification

Package: `Garmetix-Stage4D-DataConsistency-v1.2.zip`
Base package: `Garmetix-Stage4C-GSTReportsPrint-v1.1.zip`

## Goal

Stage 4D adds an admin verification layer so the project can be checked before production rollout and after every major migration/import. The checks are read-only and do not automatically modify business data.

## Backend added

New module:

- `backend/Garmetix.Api/Validation/DataConsistencyDtos.cs`
- `backend/Garmetix.Api/Validation/DataConsistencyEndpoints.cs`

New API group:

- `GET /api/data-consistency/summary`
- `GET /api/data-consistency/issues`
- `GET /api/data-consistency/csv`

All endpoints require the existing `Admin` policy.

## Checks implemented

### Inventory

- Negative stock rows.
- Stock movement ledger mismatch when a stock row has movement records.
- Stock rows pointing to missing product master rows.
- Duplicate product barcodes inside a company.
- Missing stock HSN information.

### Documents / numbering

- Duplicate sale invoice numbers.
- Duplicate purchase invoice numbers.
- Duplicate inward numbers.
- Duplicate voucher numbers.
- Duplicate cash voucher numbers.
- Duplicate credit/debit note numbers.
- Duplicate customer advance receipt numbers.
- Duplicate `DocumentSequence` rows for the same company/store/document/date scope.
- Purchase invoices missing store linkage.

### GST / item snapshots

- Sale invoice header tax totals compared with item tax totals.
- Purchase invoice header tax totals compared with item tax totals.
- CGST/SGST/IGST header totals compared with item split totals.
- Sale item snapshot completeness: product name, HSN, unit.
- Purchase item snapshot completeness: product name, HSN, unit.

### Payments

- Sale invoice `PaidAmount` compared with posted `InvoicePayment` total.
- Sale invoices where paid amount exceeds bill amount.
- Purchase invoices where allocated purchase payments exceed bill amount.
- Credit/debit notes where adjusted amount exceeds note amount.
- Customer advance receipts where adjusted amount exceeds amount.
- Customer advance available amount mismatch.

### Accounting

- Journal entries with no lines.
- Journal entries where debit and credit totals do not balance.

## Frontend added

New page:

- `frontend/garmetix-web/pages/data-consistency/index.vue`

Sidebar:

- Added **Data Consistency** under Admin menu.

The page provides:

- Run checks button.
- Severity and area filters.
- Critical/warning/info summary cards.
- Section summary by area.
- Full issue table.
- CSV export.

## Static validation script added

New script:

- `scripts/validation/stage4d-static-checks.py`

Run it from the repository root:

```bash
python scripts/validation/stage4d-static-checks.py
```

This script verifies that Stage 4D files and route registrations are present. It does not replace `dotnet build`, frontend build, or real database testing.

## Important behavior

The data consistency API is read-only. It reports issues so you can decide whether to fix them manually, write a migration, or create a controlled repair endpoint later.

## Recommended next step

Run this package in Docker/local and open:

```text
/data-consistency
```

Then fix any critical rows before moving to production or before continuing to the next business module.
