# Purchase Return Advanced Settlement Acceptance - v4.12.65

This module verifies that supplier returns are not handled by unsafe purchase deletion or manual balance edits.

## Use this page before purchase/GST closeout

Open **Purchase → Return Settlement QA** and run the report for the closing period.

The status is **Complete** only when there are no Critical or Warning issues.

## Correct business flow

1. Open the original purchase invoice.
2. Create a formal purchase return for returned items.
3. Verify stock-out movement for returned quantity.
4. Verify linked supplier debit note.
5. Verify exact ITC reversal from the original purchase tax snapshot.
6. Print/share the purchase return and debit note.
7. Settle the debit note through Vendor Settlements:
   - adjust against vendor payable invoices, or
   - record supplier refund, or
   - both.
8. Confirm purchase-return and refund journals.
9. Export CSV evidence.

## What not to do

- Do not delete a purchase inward to handle a supplier return.
- Do not manually overwrite vendor balances.
- Do not post supplier refund as normal customer receipt.
- Do not approximate ITC reversal at month end.
