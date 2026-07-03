# v4.11.93 Purchase Import Proof Checklist

Stage 11D-78 hardens the final supplier invoice import workflow after parser, discount, barcode and size-split improvements.

## What changed

### Purchase invoice list proof visibility

Purchase invoices that were posted from a supplier invoice import now return:

- `hasImportProof`
- `importBatchId`

The purchase list shows a **Supplier proof** badge beside those invoices and adds a **Proof** action button that opens the original uploaded supplier invoice through the authenticated API.

### Purchase receipt proof banner

When a purchase inward receipt is opened, the modal shows a proof-linked banner if the inward was posted from an import draft. The existing **Supplier proof** button remains available from the receipt footer.

### Ready-to-post checklist

The supplier invoice import review page now shows a final checklist before posting.

The checklist verifies:

- Vendor name exists.
- Supplier invoice number exists.
- Supplier invoice date exists.
- At least one active item row exists.
- No missing or duplicate barcode exists.
- GST/tax is assigned.
- New product defaults are complete.
- Scanned invoice total reconciles with calculated draft total.
- Header discount reconciles with distributed line discount.
- Bank account is selected for non-cash payment.

The **Post Inward** button is disabled until this checklist is ready. The draft can still be saved while issues are being corrected.

## Test checklist

1. Upload and post a supplier invoice import.
2. Open **Purchase → Purchase Invoices**.
3. Confirm the posted inward shows a **Supplier proof** badge.
4. Click **Proof** and confirm original PDF/image opens.
5. Open the purchase invoice receipt and confirm the proof-linked banner appears.
6. Create another import draft with a missing barcode/GST/category and confirm **Post Inward** is disabled.
7. Generate barcode, copy defaults, correct totals, save draft and confirm **Post Inward** becomes available.

## Notes

Posted supplier invoice proof files remain protected. Failed/rejected/unposted import history can still be cleaned from the import page, but posted proofs are retained for audit and future purchase verification.
