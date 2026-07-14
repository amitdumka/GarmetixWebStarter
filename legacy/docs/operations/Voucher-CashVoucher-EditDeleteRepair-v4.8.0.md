
# Voucher and Cash Voucher Edit/Delete Repair v4.8.0

Run normal deployment. On API startup, the app creates the missing `CashVoucherConversions` table if it is absent.

## Optional database check

```sql
SELECT to_regclass('public."CashVoucherConversions"');
```

Expected result:

```text
CashVoucherConversions
```

## User test

- Open Accounting Vouchers and edit a non-converted voucher.
- Delete a non-converted voucher.
- Open Cash Vouchers and edit a non-converted cash voucher.
- Delete a non-converted cash voucher.
