# Print/PDF Acceptance and Store Operations Landing v4.9.18

Stage 8I Package 19 hardens the final print handover workflow and changes store users' first page after login.

## Print/PDF final acceptance

`/print-final-acceptance` now checks sample availability for:

- Sales Invoice PDF
- Sales Return PDF
- Voucher PDF
- Cash Voucher PDF
- Petty Cash Sheet PDF
- Purchase Inward PDF
- Purchase Return PDF
- Debit / Credit Note PDF
- Non-GST Goods PDF
- Tailoring Order / Invoice Print
- Salary Payslip PDF
- Salary Payment Voucher PDF
- GST Return Export / CA Review

The admin acceptance page keeps the local operator checklist and notes, while the backend exposes the complete sample catalog through:

```text
GET /api/print-acceptance/status
```

## Live drill

Run this on the production host after creating at least one sample in each required area:

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/print-pdf-acceptance-drill.sh .env.production
```

The drill logs in, reads the acceptance catalog, verifies all expected document keys, and fetches ready sample endpoints with the bearer token.

## Store Operations landing

The user-facing label for `/store-day` is now **Store Operations**.

Store Manager and biller/Salesman users now land first on `/store-day` after login so the store day can be opened before billing starts.

Admin, Owner, PowerUser and Accountant users continue to land on the business dashboard. HR and Payroll users keep their dedicated landing pages.

## Validation

```bash
python3 scripts/validation/current-release-checks.py
python3 scripts/validation/print-pdf-acceptance-check.py
bash -n scripts/linux/print-pdf-acceptance-drill.sh
```
