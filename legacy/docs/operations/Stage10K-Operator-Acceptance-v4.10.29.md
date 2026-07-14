# Stage 10K Operator Acceptance - v4.10.29

Build: `GARMETIX-10K-20260620-4129`

Stage 10K adds a guided production operator rehearsal page at `/stage10k-operator-acceptance`.

## Purpose

This page is for Admin/Owner to rehearse a full store day before moving to the next development stage. It keeps the critical operator flows in one place:

- Day opening and store readiness.
- Billing and sales desk.
- Cash closing and petty cash.
- Purchase and inventory.
- Voucher, accounting and banking.
- HR attendance and payroll.
- Backup, restore and support.

## API Contract

- `GET /api/stage10k/operator-acceptance`
- `GET /api/stage10k/operator-acceptance/checklist`

Both endpoints require Admin authorization and return safe degraded payloads if an unexpected backend error occurs.

## Acceptance Evidence

During production rehearsal, record:

- Version and build code shown on the page.
- Store day open number.
- Sale invoice number and QR scan result.
- Petty cash sheet number and A5 print.
- Voucher number and bank transaction or cheque log when applicable.
- Attendance/monthly summary and payslip or salary payment slip.
- Latest backup file and restore-drill marker.
- Any related Message Log id for failed save, print or sync behavior.
