# Stage 11D-121 — Sale Invoice Barcode Print Polish

Version: 4.12.36

## Scope

- Sale invoice receipt view now shows product name and barcode together for every invoice item.
- Sale invoice browser/print/PDF output now includes barcode in the item print name for A4, A5 and thermal invoice formats.
- Sale invoice list remarks now show the first 10 characters by default, wrap safely, and can be toggled to Full/Hide per invoice row.

## Acceptance

1. Open Billing → Receipt for any sale invoice.
2. Verify each item shows both product name and `Barcode: ...`.
3. Print/download invoice PDF in A4/A5/thermal format and verify barcode is visible with item name.
4. Verify long invoice remarks wrap after the short preview and can be expanded/collapsed from the invoice list.
