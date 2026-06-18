# Stage 8H Package 3 - Purchase and Voucher Crash Hotfix (v4.7.2)

This package fixes UI/runtime crashes reported after the purchase/vendor payment stabilization release.

## Fixed

- Debit Note and Credit Note party selector no longer uses an empty SelectItem value.
- New Purchase inward vendor selector no longer uses an empty SelectItem value.
- Cash Voucher register loads the voucher list even if optional lookup/default-repair calls fail for the current role.
- Vendor Payments now uses the standard sliding add dialog instead of an always-visible inline form.
- Version advanced to 4.7.2 / GARMETIX-8H-20260617-4720.
