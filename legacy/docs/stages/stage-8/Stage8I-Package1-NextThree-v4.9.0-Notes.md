# Stage 8I Package 1 - Next Three Production Parts (v4.9.0)

This package completes the next three pending production areas after v4.8.5.

## Part 1 - Data Cleanup & Repair Dashboard

- Extended Data Consistency checks with a **Data Cleanup** area.
- Detects duplicate bank accounts by company + bank + account number.
- Detects voucher/cash-voucher/petty-cash date rows that still contain a time component from older timezone/date bugs.
- Detects vouchers that are missing their accounting journal.
- Data Consistency page now shows a Production Cleanup Focus panel.

## Part 2 - Backup/Restore Production Verification

- Backup Maintenance now includes a production backup/restore drill checklist.
- Tracks fresh backup, verify-all, off-site upload, restore preview/dry run and operator note.
- Uses browser local storage to keep the acceptance status visible for the operator.

## Part 3 - GST Final Acceptance

- Added new GST Final Acceptance page.
- Checks GST HSN rows, invoice register rows, output/input/net tax, data consistency, Brevo SMTP readiness and backup readiness.
- Adds manual acceptance checklist for Sales, Purchase, Accounting, GSTR-1, GSTR-3B, CA email, WhatsApp and backup.
- Added menu item: GST → GST Final Acceptance.

## Verification

Run:

```bash
python3 scripts/validation/stage8i-package1-static-checks.py
```
