# Stage 8H Package 14 - Voucher/Cash Voucher/Petty Cash Date Hotfix (v4.8.3)

This package fixes the one-day-back date problem in accounting forms.

## Root cause

Some frontend date fields converted an Indian local date to a UTC ISO string before saving.
For example, `2026-06-18 00:00 IST` becomes `2026-06-17T18:30:00Z`, which can be saved or displayed as the previous day.

## Included

- Voucher save now sends local midnight without UTC conversion.
- Cash Voucher save uses a shared accounting date helper.
- Petty Cash sheet save uses a shared accounting date helper.
- Voucher/Petty Cash list display no longer uses JavaScript UTC date parsing for date-only values.
- Backend normalizes Voucher and Cash Voucher dates using `.Date` before posting journal/bank/cheque records.
- Petty Cash date normalization remains active.

## Pages to test

- Vouchers
- Cash Vouchers
- Petty Cash Sheet
