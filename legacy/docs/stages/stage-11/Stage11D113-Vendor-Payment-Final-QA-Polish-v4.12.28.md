# Stage 11D-113 — Vendor Payment Final QA Polish

Version: v4.12.28  
Build: GARMETIX-11D113-20260630-4228  
Date: 2026-06-30

## Goal

Complete the practical Vendor Payment QA polish before moving to the next larger module. Vendor payment data must not be fixed/locked forever; wrong entries need safe view, edit and delete actions with linked voucher/accounting recalculation.

## Done

- Vendor Payments register now has explicit row actions:
  - View
  - Edit
  - Delete
  - Open Voucher
- Added Vendor Payment detail drawer with tabulated user-facing fields:
  - ID
  - date
  - vendor
  - invoice/advance reference
  - amount
  - payment mode
  - reference/slip
  - payment details
  - linked voucher number
  - remarks
- Added Edit Vendor Payment slideover for:
  - payment date
  - amount
  - payment mode
  - bank account when non-cash
  - reference/slip
  - payment details
  - remarks
- Delete action now uses confirmation modal and calls the existing backend safe-delete endpoint.
- After edit/delete, the page refreshes and backend recalculates vendor paid totals, purchase invoice status, voucher values and accounting/bank artifacts through existing Stage 11D-103/104 services.
- Direct deep-link support added:
  - `/vendor-payments?paymentId=<id>`
  - `/vendor-payments?vendorPaymentId=<id>`
- Day Book vendor payment source links now open the Vendor Payments page directly instead of routing through the Purchase page.
- Day Book vendor payment rows now use the actual vendor name when available.

## Live QA checklist

1. Open `/vendor-payments`.
2. Click **View** on a recent invoice-linked payment.
3. Confirm ID, vendor, amount, mode, reference and voucher number are visible.
4. Click **Edit**, change reference/remarks, save, refresh and confirm the row updates.
5. Edit a non-cash payment to Cash and confirm bank account is cleared.
6. Edit amount on a partially-paid invoice and confirm invoice balance/status remains correct.
7. Delete a test vendor payment and confirm:
   - it disappears from active register
   - linked voucher is inactive/deleted from active views
   - linked accounting/bank artifacts are not active
   - invoice status and vendor paid totals recalculate
8. From Day Book, open a Vendor Payment row using external/source link.
9. Confirm it opens `/vendor-payments?paymentId=<id>&fromDayBook=1` and the detail drawer opens.
10. Click **Back to Day Book** and confirm Day Book date/filter/search state is preserved.

## Validation

Run:

```bash
python3 scripts/validation/current-release-checks.py
```

Expected:

- Stage 11D-113 validation passed
- App version string safety passed
- Frontend route access passed
- Navigation menu coverage passed
- Secret hygiene passed

## Not included

- Full production database smoke test.
- Full `.NET publish` build in this patch environment.
- Vendor payment reconciliation repair UI. Backend reconciliation endpoints already exist and remain available for admin/API/runtime checks.
