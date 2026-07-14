
# Stage 8H Package 11 - Voucher Edit/Delete Repair (v4.8.0)

This package fixes voucher and cash voucher edit/delete failures caused by an existing Docker/PostgreSQL volume missing the `CashVoucherConversions` audit table.

## Root cause

Voucher and cash voucher edit/delete operations check `CashVoucherConversions` first so converted documents remain immutable for audit safety. Some upgraded databases had the DbSet in code but did not have the physical table, causing PostgreSQL `42P01: relation "CashVoucherConversions" does not exist`.

## Fixed

- Added idempotent startup schema repair for `CashVoucherConversions`.
- Added EF migration for normal `Database:SchemaBootstrapMode=Migrate` deployments.
- Voucher edit and delete can now run once the app restarts and repairs the table.
- Cash voucher edit and delete can now run once the app restarts and repairs the table.
- Converted voucher/cash-voucher immutability is still protected.

## Verification

After deployment, restart the stack and test:

1. Edit a normal voucher.
2. Delete a normal voucher.
3. Edit a normal cash voucher.
4. Delete a normal cash voucher.
5. Convert a cash voucher to accounting voucher and confirm converted records cannot be changed/deleted.
