# Stage 10M Production Rehearsal - v4.10.31

Build: `GARMETIX-10M-20260620-4131`

Stage 10M adds a live-data rehearsal console at `/production-rehearsal`.

## Purpose

Use this page before Stage 11 mobile/device work. The rehearsal should be run from the same public or LAN URL that operators use.

## Run Sheet

- Pre-flight and workspace.
- Store day and cash opening.
- Billing and document scan.
- Purchase, stock and import/export.
- Accounting, banking and voucher print.
- HR, attendance and payroll.
- Close, support and go/no-go.

## Issue Buckets

- Save or validation failure.
- Print or QR failure.
- Access or role mismatch.
- Hosted API or tunnel mismatch.
- Accounting or ledger mismatch.

## API Contract

- `GET /api/stage10m/production-rehearsal`
- `GET /api/stage10m/production-rehearsal/run-sheet`

Both endpoints require Admin authorization and return safe degraded payloads if an unexpected backend error occurs.

## Go/No-Go Evidence

Before Stage 11, capture:

- Version/build code.
- Store, date and operator.
- Day Open number.
- Invoice, voucher, petty cash and payroll document numbers.
- QR scan result.
- Backup and restore-drill marker.
- Message Log id for every failed save, print, access or hosted URL issue.
