# Stage 8I Package 2 - Print & Permission Final Acceptance (v4.9.1)

This package adds the next production acceptance part after v4.9.0.

## Print Final Acceptance

New page:

- **Reports → Print Final Acceptance**

Backend endpoint:

- `GET /api/print-acceptance/status`

Checks sample source availability for:

- Voucher PDF
- Cash Voucher PDF
- Petty Cash Sheet PDF
- Purchase Inward PDF
- Tailoring Order / Invoice print payload
- GST Return export / CA review

The page lets the operator open each sample and mark it as accepted after verifying logo, date, amount/tax, signatures and page size.

## Permission Final Acceptance

New page:

- **Admin → Permission Final Acceptance**

Backend endpoint:

- `GET /api/permission-acceptance/status`

Checks:

- Total users
- Active users
- Admin/Owner coverage
- Scoped company/store users
- Role coverage by login role

The page includes a manual acceptance checklist for Admin/Owner, Store Manager, Billing, Purchase, Accountant, HR/Payroll, store scoping and delete/export/backup restrictions.

## Verification

Run:

```bash
python3 scripts/validation/stage8i-package2-static-checks.py
```
