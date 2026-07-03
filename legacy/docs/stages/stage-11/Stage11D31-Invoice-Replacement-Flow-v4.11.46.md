# Stage 11D-31 / v4.11.46 - Invoice Replacement Flow

Build: `GARMETIX-11D31-20260626-4146`

## Purpose

Purchase and sale revision was already available as a safe clone workflow. This stage adds an optional replacement switch so Admin/Owner can save a revised invoice and cancel/reverse the old invoice in the same controlled flow.

## Purchase replacement

Page: `Purchase → Purchase Inward → Revise`

Flow:

1. Open an old purchase invoice.
2. Click `Revise`.
3. Correct item, quantity, cost, MRP, discount, GST, vendor or freight.
4. Turn on `After saving revised inward, cancel/reverse the original purchase invoice automatically`.
5. Save.
6. New inward is created first.
7. Old purchase invoice is then cancelled using existing cancellation/reversal posting.

If the old invoice cancellation fails, the revised inward remains saved and the old invoice can be cancelled manually.

## Sale replacement

Page: `Billing → Sales Invoices → Revise`

Flow:

1. Open an old sale invoice.
2. Click `Revise`.
3. Correct item, quantity, MRP, discount, customer or payment details.
4. Turn on `After saving revised sale invoice, cancel/reverse the original sale invoice automatically`.
5. Save.
6. New sale invoice is created first.
7. Old sale invoice is then cancelled using existing stock/payment/accounting reversal posting.

## Safety design

- The old invoice is never cancelled before the new invoice is saved.
- If replacement cancellation fails, the system does not roll back the newly saved revised invoice.
- Existing cancellation endpoints and audit triggers continue to capture the reversal.
- This is safer than editing invoice item rows in place because stock, GST, vendor/customer balance and accounting remain traceable.

## Test checklist

- Purchase revised inward save without replacement switch.
- Purchase revised inward save with replacement switch as Admin/Owner.
- Sale revised invoice save without replacement switch.
- Sale revised invoice save with replacement switch as Admin/Owner.
- Verify old invoice status is `Cancelled`.
- Verify stock movement reversal exists.
- Verify audit report records the change.
