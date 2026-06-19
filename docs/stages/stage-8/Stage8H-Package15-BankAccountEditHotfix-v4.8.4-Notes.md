# Stage 8H Package 15 - Bank Account Edit Hotfix (v4.8.4)

This package fixes the Bank Account edit issue where saving an edited bank account could create a new record instead of updating the selected record.

## Root cause

The Accounting page edit form loaded the bank account id into the form, but the save payload for Bank Accounts did not include that id. The generic save routine checks `payload.id` to decide whether to call update or create. Because the id was missing, it called create.

## Included

- Bank Account edit payload now preserves the original bank account `id`.
- Edit save is blocked if the id is missing, preventing accidental duplicate creation.
- Backend now rejects duplicate bank accounts for the same company + bank + account number.
- Existing update route remains `PUT /api/bank-accounts/{id}`.

## Verification

1. Open Accounting → Bank Accounts.
2. Note total row count.
3. Edit an existing bank account holder/branch/balance.
4. Save.
5. Confirm row count remains the same and the same record is updated.
