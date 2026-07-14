# Stage 8C Package 2 - Purchase Return Documents

Version: 4.2.1  
Date: 2026-06-15

## Delivered

- Added a dedicated colored Purchase Return and Debit Note server PDF.
- Added A4 and A5 layouts with multi-page item rendering so no return line is omitted.
- Printed original purchase, vendor GSTIN, return reason, debit-note reference, quantity, taxable value, CGST, SGST, IGST, and total debit amount.
- Added store, supplier, office, and duplicate copy labels plus signatures and reprint marking.
- Added stable `GMX:PURCHASERETURN:<id>` QR identity and permission-aware Document Scanner resolution.
- Added automatic first print after a new item-wise return is saved.
- Added direct print, reprint, download, print-state filter, and scanned-document detail opening in the Purchase Return workspace.
- Added persistent printed flag, print count, last-print time, and synchronized printed state on the linked commercial debit note.
- Added an idempotent migration and startup repair for existing databases.

## Accounting Safety

- Printing does not change the accounting `Posted` status.
- Reprints are auditable and do not duplicate stock, debit notes, journals, or vendor balances.
- Downloading a PDF does not falsely mark the document as physically printed.

## Next

Stage 8C Package 3 will add vendor refund, debit-note adjustment, and outstanding settlement allocation.
